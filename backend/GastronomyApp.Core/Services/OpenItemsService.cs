using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Core.Services;

public sealed class OpenItemsService
{
  private readonly IGastronomyDatabase _database;
  private readonly OrderItemLifecycle _itemLifecycle;
  private readonly ILogger<OpenItemsService> _logger;
  private readonly TypeAdapterConfig _mapping;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly TimeProvider _timeProvider;

  public OpenItemsService(IGastronomyDatabase database, TypeAdapterConfig mapping, RunningFestivalLookup runningFestival, OrderItemLifecycle itemLifecycle, TimeProvider timeProvider, ILogger<OpenItemsService> logger)
  {
    _database = database;
    _itemLifecycle = itemLifecycle;
    _mapping = mapping;
    _runningFestival = runningFestival;
    _timeProvider = timeProvider;
    _logger = logger;
  }

  public async Task<ErrorOr<TableOrderReportView>> ReadTableAsync(Guid tableId, CancellationToken cancellationToken)
  {
    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return Refusal.Settlement.NoRunningFestival();

    var table = await _database.Tables.AsNoTracking()
                               .Where(candidate => candidate.Id == tableId && candidate.FestivalId == festival.Id)
                               .Include(candidate => candidate.Orders.OrderByDescending(order => order.CreatedAtUtc).ThenByDescending(order => order.GlobalOrderNumber))
                               .ThenInclude(order => order.StaffMember)
                               .Include(candidate => candidate.Orders)
                               .ThenInclude(order => order.StationOrders)
                               .ThenInclude(stationOrder => stationOrder.Items)
                               .FirstOrDefaultAsync(cancellationToken);

    if (table is null)
      return Refusal.Settlement.TableNotAtTheRunningFestival(tableId);

    return table.Adapt<TableOrderReportView>(_mapping);
  }

  public async Task<ErrorOr<SettlementView>> SettleAsync(IReadOnlyList<SettleLineRequest> lines, PaymentMethod paymentMethod, Guid settledByStaffMemberId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(lines);

    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return Refusal.Settlement.NoRunningFestival();

    List<Guid> selectedIds = lines.Select(line => line.OrderItemId).Distinct().ToList();
    Dictionary<Guid, OrderItem> knownItems = await _database.OrderItems.Include(item => item.StationOrder).ThenInclude(stationOrder => stationOrder.Order).Where(item => selectedIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);

    ErrorOr<SettlementResult> settled = Settle(lines, paymentMethod, settledByStaffMemberId, knownItems, _timeProvider.GetUtcNow().UtcDateTime);

    if (settled.IsError)
      return settled.Errors;

    await _database.SaveChangesAsync(cancellationToken);

    LogSettled(settled.Value);

    return settled.Value.Adapt<SettlementView>(_mapping);
  }

  public async Task<ErrorOr<CancellationView>> CancelAsync(IReadOnlyList<Guid> orderItemIds, Guid cancelledByStaffMemberId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return Refusal.Cancellation.NoRunningFestival();

    if (cancelledByStaffMemberId == Guid.Empty)
      throw new ArgumentException("A cancelled item has to name the waiter who cancelled it.", nameof(cancelledByStaffMemberId));

    if (orderItemIds.Count == 0)
      return Refusal.Cancellation.NothingSelected();

    List<Error> duplicates = FindDuplicates(orderItemIds, Refusal.Cancellation.DuplicateOrderItemId);
    if (duplicates.Count > 0)
      return duplicates;

    Dictionary<Guid, OrderItem> knownItems = await _database.OrderItems.Include(item => item.StationOrder)
                                                            .ThenInclude(stationOrder => stationOrder.Order)
                                                            .Where(item => orderItemIds.Contains(item.Id) && item.StationOrder.FestivalId == festival.Id)
                                                            .ToDictionaryAsync(item => item.Id, cancellationToken);

    ErrorOr<List<OrderItem>> found = FindKnownItems(orderItemIds, knownItems, Refusal.Cancellation.UnknownOrderItemId);
    if (found.IsError)
      return found.Errors;

    var selected = found.Value;

    List<Error> alreadyCancelled = selected.Where(IsAlreadyCancelled).Select(item => Refusal.Cancellation.AlreadyCancelled(item.Id, item.ItemName)).ToList();
    if (alreadyCancelled.Count > 0)
      return alreadyCancelled;

    ErrorOr<Success> oneTable = EnsureOneTable(selected, Refusal.Cancellation.SelectionSpansSeveralTables);
    if (oneTable.IsError)
      return oneTable.Errors;

    var cancelledAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
    OrderItemStatusAuthor waiter = new(cancelledByStaffMemberId, null);

    foreach (var item in selected)
      _itemLifecycle.AppendStatus(item, OrderItemState.Cancelled, waiter, cancelledAtUtc);

    await _database.SaveChangesAsync(cancellationToken);

    _logger.LogInformation("{CancelledItemCount} order items were cancelled and saved. Order item ids: {CancelledOrderItemIds}.", selected.Count, selected.Select(item => item.Id).ToList());

    return new CancellationView(selected.Where(item => item.SettledAtUtc is not null).Sum(item => item.ChargedPriceCents ?? 0));
  }

  private ErrorOr<SettlementResult> Settle(IReadOnlyList<SettleLineRequest> lines, PaymentMethod paymentMethod, Guid settledByStaffMemberId, IReadOnlyDictionary<Guid, OrderItem> knownItemsById, DateTime settledAtUtc)
  {
    EnsureSettlerNamed(settledByStaffMemberId);

    List<Error> duplicates = FindDuplicates(lines.Select(line => line.OrderItemId), Refusal.Settlement.DuplicateOrderItemId);
    if (duplicates.Count > 0)
      return duplicates;

    var moneyWasPaid = lines.Any(line => line.PaidPriceCents!.Value > 0);
    if (moneyWasPaid && paymentMethod is not (PaymentMethod.Cash or PaymentMethod.Card))
      return Refusal.Settlement.PaymentMethodMissingForMoneyPaid(paymentMethod);

    if (!moneyWasPaid && paymentMethod != PaymentMethod.None)
      return Refusal.Settlement.PaymentMethodNamedForNothingPaid(paymentMethod);

    ErrorOr<List<OrderItem>> found = FindKnownItems(lines.Select(line => line.OrderItemId), knownItemsById, Refusal.Settlement.UnknownOrderItemId);
    if (found.IsError)
      return found.Errors;

    var selected = found.Value;

    List<Error> cancelled = selected.Where(item => item.IsCancelled()).Select(item => Refusal.Settlement.ItemWasCancelled(item.Id, item.ItemName)).ToList();
    if (cancelled.Count > 0)
      return cancelled;

    List<Error> missingNotices = FindMissingPaymentNotices(lines, selected);
    if (missingNotices.Count > 0)
      return missingNotices;

    ErrorOr<Success> oneTable = EnsureOneTable(selected, Refusal.Settlement.SelectionSpansSeveralTables);
    if (oneTable.IsError)
      return oneTable.Errors;

    return Apply(lines, paymentMethod, selected, settledByStaffMemberId, settledAtUtc);
  }

  private SettlementResult Apply(IReadOnlyList<SettleLineRequest> lines, PaymentMethod paymentMethod, IReadOnlyList<OrderItem> selected, Guid settledByStaffMemberId, DateTime settledAtUtc)
  {
    List<OrderItem> newlySettled = [];
    List<OrderItem> reapplied = [];
    List<OrderItem> alreadySettledByOthers = [];

    for (var index = 0; index < lines.Count; index++)
    {
      var line = lines[index];
      var item = selected[index];
      var paidPriceCents = line.PaidPriceCents!.Value;

      if (item.SettledAtUtc is null)
      {
        item.SettledAtUtc = settledAtUtc;
        item.SettledByStaffMemberId = settledByStaffMemberId;
        OverwriteChargedPrice(item, paidPriceCents, line.PaymentNotice, paymentMethod);
        newlySettled.Add(item);
        continue;
      }

      if (item.SettledByStaffMemberId == settledByStaffMemberId)
      {
        OverwriteChargedPrice(item, paidPriceCents, line.PaymentNotice, paymentMethod);
        reapplied.Add(item);
        continue;
      }

      alreadySettledByOthers.Add(item);
    }

    return new()
           {
             NewlySettled = newlySettled,
             Reapplied = reapplied,
             AlreadySettledByOthers = alreadySettledByOthers
           };
  }

  private void EnsureSettlerNamed(Guid settledByStaffMemberId)
  {
    if (settledByStaffMemberId == Guid.Empty)
      throw new ArgumentException("A settled item has to name the staff member who collected the money.", nameof(settledByStaffMemberId));
  }

  private void OverwriteChargedPrice(OrderItem item, int chargedPriceCents, string? paymentNotice, PaymentMethod paymentMethod)
  {
    item.ChargedPriceCents = chargedPriceCents;
    item.PaymentNotice = paymentNotice;

    if (string.IsNullOrEmpty(paymentNotice))
      item.PaymentNotice = null;

    item.PaymentMethod = PaymentMethod.None;

    if (chargedPriceCents > 0)
      item.PaymentMethod = paymentMethod;
  }

  private List<Error> FindDuplicates(IEnumerable<Guid> orderItemIds, Func<Guid, Error> duplicateRefusal)
  {
    HashSet<Guid> seenIds = [];
    List<Error> duplicates = [];

    foreach (var orderItemId in orderItemIds)
      if (!seenIds.Add(orderItemId))
        duplicates.Add(duplicateRefusal(orderItemId));

    return duplicates;
  }

  private ErrorOr<List<OrderItem>> FindKnownItems(IEnumerable<Guid> orderItemIds, IReadOnlyDictionary<Guid, OrderItem> knownItemsById, Func<Guid, Error> unknownRefusal)
  {
    List<OrderItem> selected = [];
    List<Error> strangers = [];

    foreach (var orderItemId in orderItemIds)
      if (knownItemsById.TryGetValue(orderItemId, out var knownItem))
        selected.Add(knownItem);
      else
        strangers.Add(unknownRefusal(orderItemId));

    if (strangers.Count > 0)
      return strangers;

    return selected;
  }

  private bool IsAlreadyCancelled(OrderItem item)
  {
    var change = _itemLifecycle.CheckStatusChange(item, OrderItemState.Cancelled);

    if (change is OrderItemStatusChange.AlreadyInThatState or OrderItemStatusChange.ItemIsCancelled)
      return true;

    if (change == OrderItemStatusChange.NotAllowed)
      throw new InvalidOperationException($"Order item {item.Id} could not be cancelled although every item that is not cancelled may be.");

    return false;
  }

  private ErrorOr<Success> EnsureOneTable(IEnumerable<OrderItem> selected, Func<IReadOnlyList<Guid>, Error> severalTablesRefusal)
  {
    IReadOnlyList<Guid> tableIds = selected.Select(item => item.StationOrder.Order.TableId).Distinct().ToList();
    if (tableIds.Count > 1)
      return severalTablesRefusal(tableIds);

    return Result.Success;
  }

  private List<Error> FindMissingPaymentNotices(IReadOnlyList<SettleLineRequest> lines, IReadOnlyList<OrderItem> selected)
  {
    List<Error> missing = [];

    for (var index = 0; index < lines.Count; index++)
    {
      var line = lines[index];

      if (line.PaidPriceCents!.Value < selected[index].UnitPriceCents && string.IsNullOrEmpty(line.PaymentNotice))
        missing.Add(Refusal.Settlement.PaymentNoticeMissing(line.OrderItemId));
    }

    return missing;
  }

  private void LogSettled(SettlementResult settlement)
  {
    List<Guid> settledIds = settlement.NewlySettled.Select(item => item.Id).ToList();

    if (settledIds.Count == 0)
      return;

    _logger.LogInformation("{SettledItemCount} order items were settled and saved. Order item ids: {SettledOrderItemIds}.", settledIds.Count, settledIds);
  }
}
