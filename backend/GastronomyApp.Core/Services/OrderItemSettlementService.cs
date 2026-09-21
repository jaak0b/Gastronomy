using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class OrderItemSettlementService
{
  private readonly TimeProvider _timeProvider;
  private readonly IOpenItemRepository _repository;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly ITransactionRunner _transactionRunner;

  public OrderItemSettlementService(IOpenItemRepository repository, RunningFestivalLookup runningFestival, ITransactionRunner transactionRunner, TimeProvider timeProvider)
  {
    _repository = repository;
    _runningFestival = runningFestival;
    _transactionRunner = transactionRunner;
    _timeProvider = timeProvider;
  }

  public async Task<Result<SettlementResult, SettlementFailure>> SettleAsync(IReadOnlyList<SettleLineRequest> lines, Guid settledByStaffMemberId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(lines);

    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return Result<SettlementResult, SettlementFailure>.Failed(new() { Reason = SettlementFailureReason.NoRunningFestival });

    return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                             {
                                               Result<SettlementResult, SettlementFailure> settlement = await SettledInsideTransactionAsync(lines, settledByStaffMemberId, transactionCancellationToken);

                                               return new TransactionOutcome<Result<SettlementResult, SettlementFailure>>
                                                      {
                                                        Value = settlement,
                                                        ShouldCommit = settlement.IsSuccess
                                                      };
                                             },
                                             cancellationToken);
  }

  public Result<SettlementResult, SettlementFailure> Settle(IReadOnlyList<SettleLineRequest> lines, Guid settledByStaffMemberId, IReadOnlyCollection<OrderItem> knownItems, DateTime settledAtUtc)
  {
    ArgumentNullException.ThrowIfNull(lines);
    ArgumentNullException.ThrowIfNull(knownItems);

    EnsureSettlerNamed(settledByStaffMemberId);

    var shapeFailure = ValidateShape(lines);
    if (shapeFailure is not null)
      return Result<SettlementResult, SettlementFailure>.Failed(shapeFailure);

    Dictionary<Guid, OrderItem> knownItemsById = knownItems.ToDictionary(item => item.Id);
    List<OrderItem> selected = [];

    foreach (var line in lines)
    {
      if (!knownItemsById.TryGetValue(line.OrderItemId, out var knownItem))
      {
        return Result<SettlementResult, SettlementFailure>.Failed(new()
                                                                  {
                                                                    Reason = SettlementFailureReason.UnknownOrderItemId,
                                                                    OffendingOrderItemId = line.OrderItemId
                                                                  });
      }

      selected.Add(knownItem);
    }

    var priceFailure = ValidatePrices(lines, selected);
    if (priceFailure is not null)
      return Result<SettlementResult, SettlementFailure>.Failed(priceFailure);

    var tableFailure = ValidateOneTable(selected);
    if (tableFailure is not null)
      return Result<SettlementResult, SettlementFailure>.Failed(tableFailure);

    List<OrderItem> newlySettled = [];
    List<OrderItem> reapplied = [];
    List<OrderItem> alreadySettledByOthers = [];
    List<string> tableNamesOfTheNewlySettled = [];

    for (var index = 0; index < lines.Count; index++)
    {
      var line = lines[index];
      var item = selected[index];
      var paidPriceCents = line.PaidPriceCents!.Value;

      if (item.SettledAtUtc is null)
      {
        MarkSettled(item, paidPriceCents, line.PaymentNotice, settledByStaffMemberId, settledAtUtc);
        newlySettled.Add(item);
        tableNamesOfTheNewlySettled.Add(TableNameOf(item));
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

    return Result<SettlementResult, SettlementFailure>.Success(new()
                                                               {
                                                                 NewlySettled = newlySettled,
                                                                 Reapplied = reapplied,
                                                                 AlreadySettledByOthers = alreadySettledByOthers,
                                                                 SettledTableNames = SortedNames(tableNamesOfTheNewlySettled)
                                                               });
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

  private async Task<Result<SettlementResult, SettlementFailure>> SettledInsideTransactionAsync(IReadOnlyList<SettleLineRequest> lines, Guid settledByStaffMemberId, CancellationToken cancellationToken)
  {
    IReadOnlyList<OrderItem> selected = await _repository.FindForSettlementAsync(ReadSelectedIds(lines), cancellationToken);

    Result<SettlementResult, SettlementFailure> settlement = Settle(lines, settledByStaffMemberId, ItemsWhoseTableIsKnown(selected), _timeProvider.GetUtcNow().UtcDateTime);

    if (settlement.IsSuccess)
      await _repository.SaveChangesAsync(cancellationToken);

    return settlement;
  }

  private IReadOnlyCollection<OrderItem> ItemsWhoseTableIsKnown(IReadOnlyCollection<OrderItem> selected)
  {
    return selected.Where(item => item.StationOrder?.Order is not null).ToList();
  }

  private string TableNameOf(OrderItem item)
  {
    return item.StationOrder.Order.TableName;
  }

  private IReadOnlyList<Guid> ReadSelectedIds(IReadOnlyList<SettleLineRequest> lines)
  {
    return lines.Select(line => line.OrderItemId).Distinct().ToList();
  }

  private IReadOnlyList<string> SortedNames(IEnumerable<string> tableNames)
  {
    return tableNames.Distinct(StringComparer.Ordinal).OrderBy(tableName => tableName, StringComparer.Ordinal).ToList();
  }

  private void EnsureSettlerNamed(Guid settledByStaffMemberId)
  {
    if (settledByStaffMemberId == Guid.Empty)
      throw new ArgumentException("A settled item has to name the staff member who collected the money.", nameof(settledByStaffMemberId));
  }

  private void OverwriteChargedPrice(OrderItem item, int chargedPriceCents, string? paymentNotice)
  {
    var written = TrimNotice(paymentNotice);

    item.ChargedPriceCents = chargedPriceCents;
    item.PaymentNotice = written;

    if (written.Length == 0)
      item.PaymentNotice = null;
  }

  private SettlementFailure? ValidateShape(IReadOnlyList<SettleLineRequest> lines)
  {
    if (lines.Count == 0)
      return new() { Reason = SettlementFailureReason.NoItemsSelected };

    HashSet<Guid> seenIds = [];

    foreach (var line in lines)
      if (!seenIds.Add(line.OrderItemId))
      {
        return new()
               {
                 Reason = SettlementFailureReason.DuplicateOrderItemId,
                 OffendingOrderItemId = line.OrderItemId
               };
      }

    return null;
  }

  private SettlementFailure? ValidatePrices(IReadOnlyList<SettleLineRequest> lines, IReadOnlyList<OrderItem> selected)
  {
    for (var index = 0; index < lines.Count; index++)
    {
      var line = lines[index];

      if (line.PaidPriceCents is not { } paidPriceCents)
      {
        return new()
               {
                 Reason = SettlementFailureReason.AmountPaidMissing,
                 OffendingOrderItemId = line.OrderItemId
               };
      }

      if (paidPriceCents < 0)
      {
        return new()
               {
                 Reason = SettlementFailureReason.AmountPaidNegative,
                 OffendingOrderItemId = line.OrderItemId
               };
      }

      if (paidPriceCents < selected[index].UnitPriceCents && TrimNotice(line.PaymentNotice).Length == 0)
      {
        return new()
               {
                 Reason = SettlementFailureReason.PaymentNoticeMissing,
                 OffendingOrderItemId = line.OrderItemId
               };
      }
    }

    return null;
  }

  private SettlementFailure? ValidateOneTable(IReadOnlyList<OrderItem> selected)
  {
    IReadOnlyList<string> tableNames = SortedNames(selected.Select(TableNameOf));

    if (tableNames.Count <= 1)
      return null;

    return new()
           {
             Reason = SettlementFailureReason.SelectionSpansSeveralTables,
             TableNamesInTheSelection = tableNames
           };
  }

  private string TrimNotice(string? paymentNotice)
  {
    return (paymentNotice ?? string.Empty).Trim();
  }
}
