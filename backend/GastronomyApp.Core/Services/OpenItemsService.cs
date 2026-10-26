using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Entities;
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
  private readonly ILogger<OpenItemsService> _logger;
  private readonly TypeAdapterConfig _mapping;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly TimeProvider _timeProvider;

  public OpenItemsService(IGastronomyDatabase database, TypeAdapterConfig mapping, RunningFestivalLookup runningFestival, TimeProvider timeProvider, ILogger<OpenItemsService> logger)
  {
    _database = database;
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
                               .ThenInclude(item => item.CurrentStatus)
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

  private ErrorOr<SettlementResult> Settle(IReadOnlyList<SettleLineRequest> lines, PaymentMethod paymentMethod, Guid settledByStaffMemberId, IReadOnlyDictionary<Guid, OrderItem> knownItemsById, DateTime settledAtUtc)
  {
    EnsureSettlerNamed(settledByStaffMemberId);

    List<Error> duplicates = FindDuplicateLines(lines);
    if (duplicates.Count > 0)
      return duplicates;

    var moneyWasPaid = lines.Any(line => line.PaidPriceCents!.Value > 0);
    if (moneyWasPaid && paymentMethod is not (PaymentMethod.Cash or PaymentMethod.Card))
      return Refusal.Settlement.PaymentMethodMissingForMoneyPaid(paymentMethod);

    if (!moneyWasPaid && paymentMethod != PaymentMethod.None)
      return Refusal.Settlement.PaymentMethodNamedForNothingPaid(paymentMethod);

    List<OrderItem> selected = [];
    List<Error> strangers = [];

    foreach (var line in lines)
      if (knownItemsById.TryGetValue(line.OrderItemId, out var knownItem))
        selected.Add(knownItem);
      else
        strangers.Add(Refusal.Settlement.UnknownOrderItemId(line.OrderItemId));

    if (strangers.Count > 0)
      return strangers;

    List<Error> missingNotices = FindMissingPaymentNotices(lines, selected);
    if (missingNotices.Count > 0)
      return missingNotices;

    IReadOnlyList<Guid> tableIds = selected.Select(item => item.StationOrder.Order.TableId).Distinct().ToList();
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

  private List<Error> FindDuplicateLines(IReadOnlyList<SettleLineRequest> lines)
  {
    HashSet<Guid> seenIds = [];
    List<Error> duplicates = [];

    foreach (var line in lines)
      if (!seenIds.Add(line.OrderItemId))
        duplicates.Add(Refusal.Settlement.DuplicateOrderItemId(line.OrderItemId));

    return duplicates;
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
