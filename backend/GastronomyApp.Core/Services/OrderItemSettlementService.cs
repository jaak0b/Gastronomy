using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Requests;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class OrderItemSettlementService
{
  private readonly IClock _clock;
  private readonly IOpenItemRepository _repository;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly ITransactionRunner _transactionRunner;

  public OrderItemSettlementService(IOpenItemRepository repository, RunningFestivalLookup runningFestival, ITransactionRunner transactionRunner, IClock clock)
  {
    _repository = repository;
    _runningFestival = runningFestival;
    _transactionRunner = transactionRunner;
    _clock = clock;
  }

  public async Task<Result<SettlementResult, SettlementFailure>> SettleAsync(SettlementRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return Result<SettlementResult, SettlementFailure>.Failed(new() { Reason = SettlementFailureReason.NoRunningFestival });

    return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                             {
                                               Result<SettlementResult, SettlementFailure> settlement = await SettledInsideTransactionAsync(request, transactionCancellationToken);

                                               return new TransactionOutcome<Result<SettlementResult, SettlementFailure>>
                                                      {
                                                        Value = settlement,
                                                        ShouldCommit = settlement.IsSuccess
                                                      };
                                             },
                                             cancellationToken);
  }

  public Result<SettlementResult, SettlementFailure> Settle(SettlementRequest request, IReadOnlyCollection<SettlementCandidate> knownItems, DateTime settledAtUtc)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(knownItems);

    EnsureSettlerNamed(request.SettledByStaffMemberId);

    var shapeFailure = ValidateShape(request.Lines);
    if (shapeFailure is not null)
      return Result<SettlementResult, SettlementFailure>.Failed(shapeFailure);

    Dictionary<Guid, SettlementCandidate> candidatesById = knownItems.ToDictionary(candidate => candidate.Item.Id);
    List<SettlementCandidate> selected = [];

    foreach (var line in request.Lines)
    {
      if (!candidatesById.TryGetValue(line.OrderItemId, out var candidate))
      {
        return Result<SettlementResult, SettlementFailure>.Failed(new()
                                                                  {
                                                                    Reason = SettlementFailureReason.UnknownOrderItemId,
                                                                    OffendingOrderItemId = line.OrderItemId
                                                                  });
      }

      selected.Add(candidate);
    }

    var priceFailure = ValidatePrices(request.Lines, selected);
    if (priceFailure is not null)
      return Result<SettlementResult, SettlementFailure>.Failed(priceFailure);

    var tableFailure = ValidateOneTable(selected);
    if (tableFailure is not null)
      return Result<SettlementResult, SettlementFailure>.Failed(tableFailure);

    List<OrderItem> newlySettled = [];
    List<OrderItem> reapplied = [];
    List<OrderItem> alreadySettledByOthers = [];
    List<string> tableNamesOfTheNewlySettled = [];

    for (var index = 0; index < request.Lines.Count; index++)
    {
      var line = request.Lines[index];
      var candidate = selected[index];
      var item = candidate.Item;
      var paidPriceCents = line.PaidPriceCents!.Value;

      if (item.SettledAtUtc is null)
      {
        MarkSettled(item, paidPriceCents, line.PaymentNotice, request.SettledByStaffMemberId, settledAtUtc);
        newlySettled.Add(item);
        tableNamesOfTheNewlySettled.Add(candidate.TableName);
        continue;
      }

      if (item.SettledByStaffMemberId == request.SettledByStaffMemberId)
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

  public int SumOpenAmountCents(IEnumerable<ISettleableOrderItem> items)
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

  private async Task<Result<SettlementResult, SettlementFailure>> SettledInsideTransactionAsync(SettlementRequest request, CancellationToken cancellationToken)
  {
    IReadOnlyList<OrderItem> selected = await _repository.FindForSettlementAsync(ReadSelectedIds(request), cancellationToken);

    IReadOnlyCollection<SettlementCandidate> candidates = await BuildCandidatesAsync(selected, cancellationToken);

    Result<SettlementResult, SettlementFailure> settlement = Settle(request, candidates, _clock.UtcNow);

    if (settlement.IsSuccess)
      await _repository.SaveChangesAsync(cancellationToken);

    return settlement;
  }

  private async Task<IReadOnlyCollection<SettlementCandidate>> BuildCandidatesAsync(IReadOnlyCollection<OrderItem> selected, CancellationToken cancellationToken)
  {
    IReadOnlyDictionary<Guid, OrderItemOwner> owners = await _repository.FindOwnersAsync(selected.Select(item => item.Id).ToList(), cancellationToken);

    return selected.Where(item => owners.ContainsKey(item.Id))
                   .Select(item => new SettlementCandidate
                                   {
                                     Item = item,
                                     TableName = owners[item.Id].TableName
                                   })
                   .ToList();
  }

  private IReadOnlyList<Guid> ReadSelectedIds(SettlementRequest request)
  {
    return request.Lines.Select(line => line.OrderItemId).Distinct().ToList();
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

  private SettlementFailure? ValidateShape(IReadOnlyList<SettlementLine> lines)
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

  private SettlementFailure? ValidatePrices(IReadOnlyList<SettlementLine> lines, IReadOnlyList<SettlementCandidate> selected)
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

      if (paidPriceCents < selected[index].Item.UnitPriceCents && TrimNotice(line.PaymentNotice).Length == 0)
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

  private SettlementFailure? ValidateOneTable(IReadOnlyList<SettlementCandidate> selected)
  {
    IReadOnlyList<string> tableNames = SortedNames(selected.Select(candidate => candidate.TableName));

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
