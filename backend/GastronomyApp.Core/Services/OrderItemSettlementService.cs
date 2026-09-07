using System.Linq.Expressions;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed record SettlementRequest
{
  public required SettlementKind Kind { get; init; }

  public required IReadOnlyList<Guid> OrderItemIds { get; init; }

  public required Guid SettledByStaffMemberId { get; init; }

  public string? PaymentNotice { get; init; }
}

public sealed class OrderItemSettlementService
{
  private const int MaximumItemsInOneSettlement = 500;

  public Result<SettlementResult, SettlementFailure> Settle(SettlementRequest request,
                                                            IReadOnlyCollection<OrderItem> knownItems,
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

    Dictionary<Guid, OrderItem> itemsById = knownItems.ToDictionary(item => item.Id);
    List<OrderItem> selected = [];

    foreach (var orderItemId in selectedIds)
    {
      if (!itemsById.TryGetValue(orderItemId, out var item))
      {
        return Result<SettlementResult, SettlementFailure>.Failed(new()
                                                                   {
                                                                     Reason = SettlementFailureReason.UnknownOrderItemId,
                                                                     OffendingOrderItemId = orderItemId
                                                                   });
      }

      selected.Add(item);
    }

    List<OrderItem> alreadySettled = [.. selected.Where(item => item.SettledAtUtc is not null)];
    List<OrderItem> stillOpen = [.. selected.Where(item => item.SettledAtUtc is null)];

    foreach (var item in stillOpen)
    {
      Apply(item, request, settledAtUtc);
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

  public void SettleAtTheDisplayedPrice(OrderItem item, Guid settledByStaffMemberId, DateTime settledAtUtc)
  {
    ArgumentNullException.ThrowIfNull(item);

    MarkSettled(item, item.UnitPriceCents, null, settledByStaffMemberId, settledAtUtc);
  }

  public void SettleFreeOfCharge(OrderItem item,
                                 string paymentNotice,
                                 Guid settledByStaffMemberId,
                                 DateTime settledAtUtc)
  {
    ArgumentNullException.ThrowIfNull(item);
    ArgumentNullException.ThrowIfNull(paymentNotice);

    MarkSettled(item, 0, paymentNotice.Trim(), settledByStaffMemberId, settledAtUtc);
  }

  private void MarkSettled(OrderItem item,
                           int chargedPriceCents,
                           string? paymentNotice,
                           Guid settledByStaffMemberId,
                           DateTime settledAtUtc)
  {
    if (settledByStaffMemberId == Guid.Empty)
    {
      throw new ArgumentException("A settled item has to name the staff member who collected the money.",
                                  nameof(settledByStaffMemberId));
    }

    item.SettledAtUtc = settledAtUtc;
    item.ChargedPriceCents = chargedPriceCents;
    item.SettledByStaffMemberId = settledByStaffMemberId;
    item.PaymentNotice = paymentNotice;
  }

  private void Apply(OrderItem item, SettlementRequest request, DateTime settledAtUtc)
  {
    switch (request.Kind)
    {
      case SettlementKind.AtTheDisplayedPrice:
        SettleAtTheDisplayedPrice(item, request.SettledByStaffMemberId, settledAtUtc);
        return;
      case SettlementKind.FreeOfCharge:
        SettleFreeOfCharge(item,
                           request.PaymentNotice ?? string.Empty,
                           request.SettledByStaffMemberId,
                           settledAtUtc);
        return;
      default:
        new Never().OfType<SettlementKind>(request.Kind);
        return;
    }
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

    return request.Kind switch
           {
             SettlementKind.AtTheDisplayedPrice => null,
             SettlementKind.FreeOfCharge => ValidatePaymentNotice(request.PaymentNotice),
             _ => new Never().OfType<SettlementFailure?>(request.Kind)
           };
  }

  private SettlementFailure? ValidatePaymentNotice(string? paymentNotice)
  {
    var written = (paymentNotice ?? string.Empty).Trim();

    if (written.Length == 0)
    {
      return new() { Reason = SettlementFailureReason.PaymentNoticeMissing };
    }

    return null;
  }
}
