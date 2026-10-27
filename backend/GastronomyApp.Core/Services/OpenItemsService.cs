using System.Diagnostics;
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

  public async Task<ErrorOr<TableOrderReportView>> SearchTableAsync(Guid tableId, OrderStatus? orderStatus, PaymentStatus? paymentStatus, bool? includeCancelled, CancellationToken cancellationToken)
  {
    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return Refusal.Settlement.NoRunningFestival();

    var table = await _database.Tables.AsNoTracking()
                               .Where(candidate => candidate.Id == tableId && candidate.FestivalId == festival.Id)
                               .Include(candidate => candidate.Orders.Where(order => (orderStatus == null || order.Status() == orderStatus) && order.Items().Any(item => item.MatchesTableSearch(paymentStatus, includeCancelled)))
                                                              .OrderByDescending(order => order.CreatedAtUtc)
                                                              .ThenByDescending(order => order.GlobalOrderNumber))
                               .ThenInclude(order => order.StaffMember)
                               .Include(candidate => candidate.Orders)
                               .ThenInclude(order => order.StationOrders)
                               .ThenInclude(stationOrder => stationOrder.Items.Where(item => item.MatchesTableSearch(paymentStatus, includeCancelled)))
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

    List<Guid> duplicateIds = DuplicatesIn(orderItemIds);
    if (duplicateIds.Count > 0)
      return duplicateIds.Select(Refusal.Cancellation.DuplicateOrderItemId).ToList();

    Dictionary<Guid, OrderItem> knownItems = await _database.OrderItems.Include(item => item.StationOrder)
                                                            .ThenInclude(stationOrder => stationOrder.Order)
                                                            .Where(item => orderItemIds.Contains(item.Id) && item.StationOrder.FestivalId == festival.Id)
                                                            .ToDictionaryAsync(item => item.Id, cancellationToken);

    List<Guid> unknownIds = orderItemIds.Where(orderItemId => !knownItems.ContainsKey(orderItemId)).ToList();
    if (unknownIds.Count > 0)
      return unknownIds.Select(Refusal.Cancellation.UnknownOrderItemId).ToList();

    List<OrderItem> selected = orderItemIds.Select(orderItemId => knownItems[orderItemId]).ToList();

    List<Error> notCancellable = RefusalsOfCancelling(selected);
    if (notCancellable.Count > 0)
      return notCancellable;

    List<Guid> tableIds = TableIdsOf(selected);
    if (tableIds.Count > 1)
      return Refusal.Cancellation.SelectionSpansSeveralTables(tableIds);

    var cancelledAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
    OrderItemStatusAuthor waiter = new(cancelledByStaffMemberId, null);

    foreach (var item in selected)
      _itemLifecycle.AppendStatus(item, OrderItemState.Cancelled, waiter, cancelledAtUtc);

    await _database.SaveChangesAsync(cancellationToken);

    _logger.LogInformation("{CancelledItemCount} order items were cancelled and saved. Order item ids: {CancelledOrderItemIds}.", selected.Count, selected.Select(item => item.Id).ToList());

    var cashToReturnCents = selected.Where(item => item.IsSettled()).Sum(item => item.ChargedPriceCents ?? 0);

    return new CancellationView(cashToReturnCents);
  }

  private List<Error> RefusalsOfCancelling(IEnumerable<OrderItem> selected)
  {
    List<Error> refusals = [];

    foreach (var item in selected)
    {
      switch (_itemLifecycle.JudgeChange(item, OrderItemState.Cancelled))
      {
        case OrderItemChangeVerdict.Allowed:
          break;
        case OrderItemChangeVerdict.AlreadyInThatState:
        case OrderItemChangeVerdict.ItemIsCancelled:
          refusals.Add(Refusal.Cancellation.AlreadyCancelled(item.Id, item.ItemName));
          break;
        default:
          throw new UnreachableException($"The lifecycle refused to cancel order item {item.Id} for a reason that does not apply to cancelling a placed item.");
      }
    }

    return refusals;
  }

  private ErrorOr<SettlementResult> Settle(IReadOnlyList<SettleLineRequest> lines, PaymentMethod paymentMethod, Guid settledByStaffMemberId, IReadOnlyDictionary<Guid, OrderItem> knownItemsById, DateTime settledAtUtc)
  {
    EnsureSettlerNamed(settledByStaffMemberId);

    List<Guid> duplicateIds = DuplicatesIn(lines.Select(line => line.OrderItemId));
    if (duplicateIds.Count > 0)
      return duplicateIds.Select(Refusal.Settlement.DuplicateOrderItemId).ToList();

    var moneyWasPaid = lines.Any(line => line.PaidPriceCents!.Value > 0);
    if (moneyWasPaid && paymentMethod is not (PaymentMethod.Cash or PaymentMethod.Card))
      return Refusal.Settlement.PaymentMethodMissingForMoneyPaid(paymentMethod);

    if (!moneyWasPaid && paymentMethod != PaymentMethod.None)
      return Refusal.Settlement.PaymentMethodNamedForNothingPaid(paymentMethod);

    List<Guid> unknownIds = lines.Select(line => line.OrderItemId).Where(orderItemId => !knownItemsById.ContainsKey(orderItemId)).ToList();
    if (unknownIds.Count > 0)
      return unknownIds.Select(Refusal.Settlement.UnknownOrderItemId).ToList();

    List<OrderItem> selected = lines.Select(line => knownItemsById[line.OrderItemId]).ToList();

    List<Error> cancelled = selected.Where(item => item.IsCancelled()).Select(item => Refusal.Settlement.ItemWasCancelled(item.Id, item.ItemName)).ToList();
    if (cancelled.Count > 0)
      return cancelled;

    List<Error> missingNotices = FindMissingPaymentNotices(lines, selected);
    if (missingNotices.Count > 0)
      return missingNotices;

    List<Guid> tableIds = TableIdsOf(selected);
    if (tableIds.Count > 1)
      return Refusal.Settlement.SelectionSpansSeveralTables(tableIds);

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

      if (!item.IsSettled())
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

  private List<Guid> DuplicatesIn(IEnumerable<Guid> orderItemIds)
  {
    HashSet<Guid> seenIds = [];
    List<Guid> duplicateIds = [];

    foreach (var orderItemId in orderItemIds)
      if (!seenIds.Add(orderItemId))
        duplicateIds.Add(orderItemId);

    return duplicateIds;
  }

  private List<Guid> TableIdsOf(IEnumerable<OrderItem> items)
  {
    return items.Select(item => item.StationOrder.Order.TableId).Distinct().ToList();
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
