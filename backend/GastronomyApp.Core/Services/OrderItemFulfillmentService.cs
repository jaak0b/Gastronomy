using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class OrderItemFulfillmentService
{
  public Result<IReadOnlyList<StationOrder>, FulfillmentFailure> Fulfill(IReadOnlyList<Guid> orderItemIds, IReadOnlyCollection<OrderItem> knownItems, DateTime fulfilledAtUtc)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);
    ArgumentNullException.ThrowIfNull(knownItems);

    List<Guid> selectedIds = orderItemIds.Distinct().ToList();

    Dictionary<Guid, OrderItem> itemsById = knownItems.ToDictionary(item => item.Id);
    List<OrderItem> selectedItems = [];

    foreach (var orderItemId in selectedIds)
    {
      if (!itemsById.TryGetValue(orderItemId, out var item))
        return Refuse(FulfillmentFailureReason.UnknownOrderItemId, orderItemId);

      selectedItems.Add(item);
    }

    foreach (var item in selectedItems.Where(item => item.FulfilledAtUtc is null))
      item.FulfilledAtUtc = fulfilledAtUtc;

    return Result<IReadOnlyList<StationOrder>, FulfillmentFailure>.Success(StationOrdersOf(selectedItems));
  }

  public Result<IReadOnlyList<StationOrder>, FulfillmentFailure> Unfulfill(IReadOnlyList<Guid> orderItemIds, IReadOnlyCollection<OrderItem> knownItems)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);
    ArgumentNullException.ThrowIfNull(knownItems);

    List<Guid> selectedIds = orderItemIds.Distinct().ToList();

    Dictionary<Guid, OrderItem> itemsById = knownItems.ToDictionary(item => item.Id);
    List<OrderItem> toClear = [];

    foreach (var orderItemId in selectedIds)
    {
      if (!itemsById.TryGetValue(orderItemId, out var item))
        return Refuse(FulfillmentFailureReason.UnknownOrderItemId, orderItemId);

      if (item.FulfilledAtUtc is null)
        return Refuse(FulfillmentFailureReason.ItemNotFulfilled, item.Id);

      toClear.Add(item);
    }

    foreach (var item in toClear)
      item.FulfilledAtUtc = null;

    return Result<IReadOnlyList<StationOrder>, FulfillmentFailure>.Success(StationOrdersOf(toClear));
  }

  private Result<IReadOnlyList<StationOrder>, FulfillmentFailure> Refuse(FulfillmentFailureReason reason, Guid? offendingOrderItemId)
  {
    return Result<IReadOnlyList<StationOrder>, FulfillmentFailure>.Failed(new()
                                                                          {
                                                                            Reason = reason,
                                                                            OffendingOrderItemId = offendingOrderItemId
                                                                          });
  }

  private IReadOnlyList<StationOrder> StationOrdersOf(IEnumerable<OrderItem> items)
  {
    return items.Select(item => item.StationOrder).DistinctBy(stationOrder => stationOrder.Id).ToList();
  }
}
