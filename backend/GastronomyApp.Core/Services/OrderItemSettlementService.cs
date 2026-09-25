using ErrorOr;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Core.Services;

public sealed class OrderItemSettlementService
{
  private readonly TimeProvider _timeProvider;
  private readonly ILogger<OrderItemSettlementService> _logger;
  private readonly IOpenItemRepository _repository;
  private readonly RunningFestivalLookup _runningFestival;

  public OrderItemSettlementService(IOpenItemRepository repository, RunningFestivalLookup runningFestival, TimeProvider timeProvider, ILogger<OrderItemSettlementService> logger)
  {
    _repository = repository;
    _runningFestival = runningFestival;
    _timeProvider = timeProvider;
    _logger = logger;
  }

  public async Task<ErrorOr<SettlementResult>> SettleAsync(IReadOnlyList<SettleLineRequest> lines, Guid settledByStaffMemberId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(lines);

    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return Refusal.Settlement.NoRunningFestival();

    IReadOnlyList<OrderItem> selected = await _repository.FindForSettlementAsync(lines.Select(line => line.OrderItemId).Distinct().ToList(), cancellationToken);

    ErrorOr<SettlementResult> settled = await Settle(lines, settledByStaffMemberId, selected.Where(item => item.StationOrder?.Order is not null).ToList(), _timeProvider.GetUtcNow().UtcDateTime).ThenDoAsync(settlement => _repository.SaveChangesAsync(cancellationToken));

    if (settled.IsError)
      return settled.Errors;

    LogSettled(settled.Value);

    return settled.Value;
  }

  private void LogSettled(SettlementResult settlement)
  {
    List<Guid> settledIds = settlement.NewlySettled.Select(item => item.Id).ToList();

    if (settledIds.Count == 0)
      return;

    _logger.LogInformation("{SettledItemCount} order items were settled and saved. Order item ids: {SettledOrderItemIds}.", settledIds.Count, settledIds);
  }

  public ErrorOr<SettlementResult> Settle(IReadOnlyList<SettleLineRequest> lines, Guid settledByStaffMemberId, IReadOnlyCollection<OrderItem> knownItems, DateTime settledAtUtc)
  {
    ArgumentNullException.ThrowIfNull(lines);
    ArgumentNullException.ThrowIfNull(knownItems);

    EnsureSettlerNamed(settledByStaffMemberId);

    List<Error> duplicates = FindDuplicateLines(lines);
    if (duplicates.Count > 0)
      return duplicates;

    Dictionary<Guid, OrderItem> knownItemsById = knownItems.ToDictionary(item => item.Id);
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

    IReadOnlyList<string> tableNames = selected.Select(item => item.StationOrder.Order.TableName).Distinct(StringComparer.Ordinal).OrderBy(tableName => tableName, StringComparer.Ordinal).ToList();
    if (tableNames.Count > 1)
      return Refusal.Settlement.SelectionSpansSeveralTables(tableNames);

    return Apply(lines, selected, settledByStaffMemberId, settledAtUtc);
  }

  public int SumOpenAmountCents(IEnumerable<OrderItem> items)
  {
    ArgumentNullException.ThrowIfNull(items);

    return items.Where(item => item.SettledAtUtc is null).Sum(item => item.UnitPriceCents);
  }

  public void MarkSettled(OrderItem item, int chargedPriceCents, string? paymentNotice, Guid settledByStaffMemberId, DateTime settledAtUtc)
  {
    ArgumentNullException.ThrowIfNull(item);
    EnsureSettlerNamed(settledByStaffMemberId);

    item.SettledAtUtc = settledAtUtc;
    item.SettledByStaffMemberId = settledByStaffMemberId;
    OverwriteChargedPrice(item, chargedPriceCents, paymentNotice);
  }

  private SettlementResult Apply(IReadOnlyList<SettleLineRequest> lines, IReadOnlyList<OrderItem> selected, Guid settledByStaffMemberId, DateTime settledAtUtc)
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
        MarkSettled(item, paidPriceCents, line.PaymentNotice, settledByStaffMemberId, settledAtUtc);
        newlySettled.Add(item);
        continue;
      }

      if (item.SettledByStaffMemberId == settledByStaffMemberId)
      {
        OverwriteChargedPrice(item, paidPriceCents, line.PaymentNotice);
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

  private void OverwriteChargedPrice(OrderItem item, int chargedPriceCents, string? paymentNotice)
  {
    var written = (paymentNotice ?? string.Empty).Trim();

    item.ChargedPriceCents = chargedPriceCents;
    item.PaymentNotice = written;

    if (written.Length == 0)
      item.PaymentNotice = null;
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

      if (line.PaidPriceCents!.Value < selected[index].UnitPriceCents && (line.PaymentNotice ?? string.Empty).Trim().Length == 0)
        missing.Add(Refusal.Settlement.PaymentNoticeMissing(line.OrderItemId));
    }

    return missing;
  }
}
