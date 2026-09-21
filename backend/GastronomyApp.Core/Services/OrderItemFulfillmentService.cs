using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class OrderItemFulfillmentService
{
  public Result<FulfillmentResult, FulfillmentFailure> Fulfill(IReadOnlyList<Guid> orderItemIds, IReadOnlyCollection<OrderItem> knownItems, DateTime fulfilledAtUtc)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);
    ArgumentNullException.ThrowIfNull(knownItems);

    List<Guid> selectedIds = orderItemIds.Distinct().ToList();

    if (selectedIds.Count == 0)
      return Result<FulfillmentResult, FulfillmentFailure>.Failed(new() { Reason = FulfillmentFailureReason.NoItemsSelected });

    Dictionary<Guid, OrderItem> itemsById = knownItems.ToDictionary(item => item.Id);
    List<OrderItem> toFulfill = [];
    List<OrderItem> alreadyFulfilled = [];

    foreach (var orderItemId in selectedIds)
    {
      if (!itemsById.TryGetValue(orderItemId, out var item))
      {
        return Result<FulfillmentResult, FulfillmentFailure>.Failed(new()
                                                                    {
                                                                      Reason = FulfillmentFailureReason.UnknownOrderItemId,
                                                                      OffendingOrderItemId = orderItemId
                                                                    });
      }

      if (item.FulfilledAtUtc is null)
        toFulfill.Add(item);
      else
        alreadyFulfilled.Add(item);
    }

    foreach (var item in toFulfill)
      item.FulfilledAtUtc = fulfilledAtUtc;

    return Result<FulfillmentResult, FulfillmentFailure>.Success(new()
                                                                 {
                                                                   ChangedItems = toFulfill,
                                                                   AlreadyFulfilled = alreadyFulfilled
                                                                 });
  }

  public Result<FulfillmentResult, FulfillmentFailure> Unfulfill(IReadOnlyList<Guid> orderItemIds, IReadOnlyCollection<OrderItem> knownItems)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);
    ArgumentNullException.ThrowIfNull(knownItems);

    List<Guid> selectedIds = orderItemIds.Distinct().ToList();

    if (selectedIds.Count == 0)
      return Result<FulfillmentResult, FulfillmentFailure>.Failed(new() { Reason = FulfillmentFailureReason.NoItemsSelected });

    Dictionary<Guid, OrderItem> itemsById = knownItems.ToDictionary(item => item.Id);
    List<OrderItem> toClear = [];

    foreach (var orderItemId in selectedIds)
    {
      if (!itemsById.TryGetValue(orderItemId, out var item))
      {
        return Result<FulfillmentResult, FulfillmentFailure>.Failed(new()
                                                                    {
                                                                      Reason = FulfillmentFailureReason.UnknownOrderItemId,
                                                                      OffendingOrderItemId = orderItemId
                                                                    });
      }

      if (item.FulfilledAtUtc is null)
      {
        return Result<FulfillmentResult, FulfillmentFailure>.Failed(new()
                                                                    {
                                                                      Reason = FulfillmentFailureReason.ItemNotFulfilled,
                                                                      OffendingOrderItemId = item.Id
                                                                    });
      }

      toClear.Add(item);
    }

    foreach (var item in toClear)
      item.FulfilledAtUtc = null;

    return Result<FulfillmentResult, FulfillmentFailure>.Success(new()
                                                                 {
                                                                   ChangedItems = toClear,
                                                                   AlreadyFulfilled = []
                                                                 });
  }
}
