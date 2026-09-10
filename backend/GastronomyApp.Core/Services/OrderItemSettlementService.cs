using System.Linq.Expressions;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed record SettlementRequest
{
  public required IReadOnlyList<Guid> OrderItemIds { get; init; }

  public required int AmountPaidCents { get; init; }

  public required Guid SettledByStaffMemberId { get; init; }

  public string? PaymentNotice { get; init; }
}

public sealed record SettlementCandidate
{
  public required OrderItem Item { get; init; }

  public required string TableName { get; init; }
}

public sealed class OrderItemSettlementService
{
  private const int MaximumItemsInOneSettlement = 500;

  public Result<SettlementResult, SettlementFailure> Settle(SettlementRequest request,
                                                            IReadOnlyCollection<SettlementCandidate> knownItems,
                                                            DateTime settledAtUtc)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(knownItems);

    IReadOnlyList<Guid> selectedIds = SelectedIdsOf(request);

    var shapeFailure = ValidateShape(request, selectedIds);
    if (shapeFailure is not null)
    {
      return Result<SettlementResult, SettlementFailure>.Failed(shapeFailure);
    }

    Dictionary<Guid, SettlementCandidate> candidatesById = knownItems.ToDictionary(candidate => candidate.Item.Id);
    List<SettlementCandidate> selected = [];

    foreach (var orderItemId in selectedIds)
    {
      if (!candidatesById.TryGetValue(orderItemId, out var candidate))
      {
        return Result<SettlementResult, SettlementFailure>.Failed(new()
                                                                   {
                                                                     Reason = SettlementFailureReason.UnknownOrderItemId,
                                                                     OffendingOrderItemId = orderItemId
                                                                   });
      }

      selected.Add(candidate);
    }

    List<OrderItem> alreadySettled = [.. ItemsOf(selected.Where(candidate => candidate.Item.SettledAtUtc is not null))];
    List<SettlementCandidate> stillOpenCandidates = [.. selected.Where(candidate => candidate.Item.SettledAtUtc is null)];

    var tableFailure = ValidateOneTable(stillOpenCandidates);
    if (tableFailure is not null)
    {
      return Result<SettlementResult, SettlementFailure>.Failed(tableFailure);
    }

    List<OrderItem> stillOpen = [.. ItemsOf(stillOpenCandidates)];

    var noticeFailure = ValidatePaymentNotice(request, DisplayedTotalCentsOf(stillOpen));
    if (noticeFailure is not null)
    {
      return Result<SettlementResult, SettlementFailure>.Failed(noticeFailure);
    }

    IReadOnlyList<int> chargedAmounts = ChargedAmountsOf(stillOpen, request.AmountPaidCents);

    for (var index = 0; index < stillOpen.Count; index++)
    {
      MarkSettled(stillOpen[index],
                  chargedAmounts[index],
                  request.PaymentNotice,
                  request.SettledByStaffMemberId,
                  settledAtUtc);
    }

    return Result<SettlementResult, SettlementFailure>.Success(new()
                                                                {
                                                                  NewlySettled = stillOpen,
                                                                  AlreadySettledBeforehand = alreadySettled
                                                                });
  }

  public IReadOnlyList<Guid> SelectedIdsOf(SettlementRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);

    return [.. request.OrderItemIds.Distinct()];
  }

  public int OpenAmountCentsOf(IEnumerable<OrderItem> items)
  {
    ArgumentNullException.ThrowIfNull(items);

    return items.Where(item => item.SettledAtUtc is null).Sum(item => item.UnitPriceCents);
  }

  public int WaivedAmountCentsOf(OrderItem item)
  {
    ArgumentNullException.ThrowIfNull(item);

    return item.SettledAtUtc is null
             ? 0
             : item.UnitPriceCents - (item.ChargedPriceCents ?? item.UnitPriceCents);
  }

  public int WaivedAmountCentsOf(IEnumerable<OrderItem> items)
  {
    ArgumentNullException.ThrowIfNull(items);

    return items.Sum(WaivedAmountCentsOf);
  }

  public Expression<Func<OrderItem, bool>> WasGivenAwaySince(DateTime settledFromUtc)
  {
    return item => item.SettledAtUtc != null
                && item.SettledAtUtc >= settledFromUtc
                && item.ChargedPriceCents != null
                && item.ChargedPriceCents < item.UnitPriceCents;
  }

  public void MarkSettled(OrderItem item,
                          int chargedPriceCents,
                          string? paymentNotice,
                          Guid settledByStaffMemberId,
                          DateTime settledAtUtc)
  {
    ArgumentNullException.ThrowIfNull(item);

    if (settledByStaffMemberId == Guid.Empty)
    {
      throw new ArgumentException("A settled item has to name the staff member who collected the money.",
                                  nameof(settledByStaffMemberId));
    }

    var written = NoticeWrittenIn(paymentNotice);

    item.SettledAtUtc = settledAtUtc;
    item.ChargedPriceCents = chargedPriceCents;
    item.SettledByStaffMemberId = settledByStaffMemberId;
    item.PaymentNotice = written.Length == 0 ? null : written;
  }

  private IReadOnlyList<int> ChargedAmountsOf(IReadOnlyList<OrderItem> items, int amountPaidCents)
  {
    if (items.Count == 0)
    {
      return [];
    }

    var displayedTotalCents = DisplayedTotalCentsOf(items);
    int[] chargedAmounts = new int[items.Count];

    for (var index = 0; index < items.Count; index++)
    {
      chargedAmounts[index] = displayedTotalCents > 0
                                ? (int)((long)amountPaidCents * items[index].UnitPriceCents / displayedTotalCents)
                                : amountPaidCents / items.Count;
    }

    var centsLeftToHandOut = amountPaidCents - chargedAmounts.Sum();

    for (var index = 0; index < centsLeftToHandOut; index++)
    {
      chargedAmounts[index % items.Count]++;
    }

    return chargedAmounts;
  }

  private int DisplayedTotalCentsOf(IReadOnlyList<OrderItem> items)
  {
    return items.Sum(item => item.UnitPriceCents);
  }

  private SettlementFailure? ValidateShape(SettlementRequest request, IReadOnlyList<Guid> selectedIds)
  {
    if (selectedIds.Count == 0)
    {
      return new() { Reason = SettlementFailureReason.NoItemsSelected };
    }

    if (selectedIds.Count > MaximumItemsInOneSettlement)
    {
      return new() { Reason = SettlementFailureReason.TooManyItemsSelected };
    }

    if (request.AmountPaidCents < 0)
    {
      return new() { Reason = SettlementFailureReason.AmountPaidNegative };
    }

    return null;
  }

  private IEnumerable<OrderItem> ItemsOf(IEnumerable<SettlementCandidate> candidates)
  {
    return candidates.Select(candidate => candidate.Item);
  }

  private SettlementFailure? ValidateOneTable(IReadOnlyList<SettlementCandidate> stillOpen)
  {
    List<string> tableNames =
    [
      .. stillOpen.Select(candidate => candidate.TableName)
                  .Distinct(StringComparer.Ordinal)
                  .OrderBy(tableName => tableName, StringComparer.Ordinal)
    ];

    if (tableNames.Count <= 1)
    {
      return null;
    }

    return new()
           {
             Reason = SettlementFailureReason.SelectionSpansSeveralTables,
             TableNamesInTheSelection = tableNames
           };
  }

  private SettlementFailure? ValidatePaymentNotice(SettlementRequest request, int displayedTotalCents)
  {
    if (request.AmountPaidCents >= displayedTotalCents)
    {
      return null;
    }

    if (NoticeWrittenIn(request.PaymentNotice).Length == 0)
    {
      return new() { Reason = SettlementFailureReason.PaymentNoticeMissing };
    }

    return null;
  }

  private string NoticeWrittenIn(string? paymentNotice)
  {
    return (paymentNotice ?? string.Empty).Trim();
  }
}
