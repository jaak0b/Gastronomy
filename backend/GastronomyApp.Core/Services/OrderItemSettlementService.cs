using System.Linq.Expressions;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class OrderItemSettlementService
{
  public Result<SettlementResult, SettlementFailure> Settle(SettlementRequest request,
                                                            IReadOnlyCollection<SettlementCandidate> knownItems,
                                                            DateTime settledAtUtc)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(knownItems);

    EnsureSettlerNamed(request.SettledByStaffMemberId);

    var shapeFailure = ValidateShape(request.Lines);
    if (shapeFailure is not null)
    {
      return Result<SettlementResult, SettlementFailure>.Failed(shapeFailure);
    }

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
    {
      return Result<SettlementResult, SettlementFailure>.Failed(priceFailure);
    }

    var tableFailure = ValidateOneTable(selected);
    if (tableFailure is not null)
    {
      return Result<SettlementResult, SettlementFailure>.Failed(tableFailure);
    }

    List<OrderItem> newlySettled = [];
    List<OrderItem> reapplied = [];
    List<OrderItem> alreadySettledByOthers = [];

    for (var index = 0; index < request.Lines.Count; index++)
    {
      var line = request.Lines[index];
      var item = selected[index].Item;
      var paidPriceCents = line.PaidPriceCents!.Value;

      if (item.SettledAtUtc is null)
      {
        MarkSettled(item, paidPriceCents, line.PaymentNotice, request.SettledByStaffMemberId, settledAtUtc);
        newlySettled.Add(item);
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
                                                                 AlreadySettledByOthers = alreadySettledByOthers
                                                               });
  }

  public IReadOnlyList<Guid> ReadSelectedIds(SettlementRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);

    return [.. request.Lines.Select(line => line.OrderItemId).Distinct()];
  }

  public int SumOpenAmountCents(IEnumerable<OrderItem> items)
  {
    ArgumentNullException.ThrowIfNull(items);

    return items.Where(item => item.SettledAtUtc is null).Sum(item => item.UnitPriceCents);
  }

  public int CalculateWaivedAmountCents(OrderItem item)
  {
    ArgumentNullException.ThrowIfNull(item);

    return item.SettledAtUtc is null
             ? 0
             : item.UnitPriceCents - (item.ChargedPriceCents ?? item.UnitPriceCents);
  }

  public int SumWaivedAmountCents(IEnumerable<OrderItem> items)
  {
    ArgumentNullException.ThrowIfNull(items);

    return items.Sum(CalculateWaivedAmountCents);
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
    EnsureSettlerNamed(settledByStaffMemberId);

    item.SettledAtUtc = settledAtUtc;
    item.SettledByStaffMemberId = settledByStaffMemberId;
    OverwriteChargedPrice(item, chargedPriceCents, paymentNotice);
  }

  private void EnsureSettlerNamed(Guid settledByStaffMemberId)
  {
    if (settledByStaffMemberId == Guid.Empty)
    {
      throw new ArgumentException("A settled item has to name the staff member who collected the money.",
                                  nameof(settledByStaffMemberId));
    }
  }

  private void OverwriteChargedPrice(OrderItem item, int chargedPriceCents, string? paymentNotice)
  {
    var written = NoticeWrittenIn(paymentNotice);

    item.ChargedPriceCents = chargedPriceCents;
    item.PaymentNotice = written.Length == 0 ? null : written;
  }

  private SettlementFailure? ValidateShape(IReadOnlyList<SettlementLine> lines)
  {
    if (lines.Count == 0)
    {
      return new() { Reason = SettlementFailureReason.NoItemsSelected };
    }

    HashSet<Guid> seenIds = [];

    foreach (var line in lines)
    {
      if (!seenIds.Add(line.OrderItemId))
      {
        return new()
               {
                 Reason = SettlementFailureReason.DuplicateOrderItemId,
                 OffendingOrderItemId = line.OrderItemId
               };
      }
    }

    return null;
  }

  private SettlementFailure? ValidatePrices(IReadOnlyList<SettlementLine> lines,
                                            IReadOnlyList<SettlementCandidate> selected)
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

      if (paidPriceCents < selected[index].Item.UnitPriceCents && NoticeWrittenIn(line.PaymentNotice).Length == 0)
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
    List<string> tableNames =
    [
      .. selected.Select(candidate => candidate.TableName)
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

  private string NoticeWrittenIn(string? paymentNotice)
  {
    return (paymentNotice ?? string.Empty).Trim();
  }
}
