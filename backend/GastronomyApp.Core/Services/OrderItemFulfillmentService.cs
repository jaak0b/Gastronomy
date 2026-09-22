using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class OrderItemFulfillmentService
{
  public ErrorOr<IReadOnlyList<StationOrder>> Fulfill(IReadOnlyList<Guid> orderItemIds, IReadOnlyCollection<OrderItem> knownItems, DateTime fulfilledAtUtc)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);
    ArgumentNullException.ThrowIfNull(knownItems);

    Dictionary<Guid, OrderItem> itemsById = knownItems.ToDictionary(item => item.Id);
    List<OrderItem> selectedItems = [];
    List<Error> refusedIds = [];

    foreach (var orderItemId in orderItemIds.Distinct())
      if (itemsById.TryGetValue(orderItemId, out var item))
        selectedItems.Add(item);
      else
        refusedIds.Add(Refusal.StationQueue.UnknownOrderItemId(orderItemId));

    if (refusedIds.Count > 0)
      return refusedIds;

    foreach (var item in selectedItems.Where(item => item.FulfilledAtUtc is null))
      item.FulfilledAtUtc = fulfilledAtUtc;

    return selectedItems.Select(item => item.StationOrder).DistinctBy(stationOrder => stationOrder.Id).ToList().ToErrorOr<IReadOnlyList<StationOrder>>();
  }

  public ErrorOr<IReadOnlyList<StationOrder>> Unfulfill(IReadOnlyList<Guid> orderItemIds, IReadOnlyCollection<OrderItem> knownItems)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);
    ArgumentNullException.ThrowIfNull(knownItems);

    Dictionary<Guid, OrderItem> itemsById = knownItems.ToDictionary(item => item.Id);
    List<OrderItem> toClear = [];
    List<Error> refusedIds = [];

    foreach (var orderItemId in orderItemIds.Distinct())
    {
      if (!itemsById.TryGetValue(orderItemId, out var item))
      {
        refusedIds.Add(Refusal.StationQueue.UnknownOrderItemId(orderItemId));
        continue;
      }

      if (item.FulfilledAtUtc is null)
      {
        refusedIds.Add(Refusal.StationQueue.ItemNotFulfilled(item.Id));
        continue;
      }

      toClear.Add(item);
    }

    if (refusedIds.Count > 0)
      return refusedIds;

    foreach (var item in toClear)
      item.FulfilledAtUtc = null;

    return toClear.Select(item => item.StationOrder).DistinctBy(stationOrder => stationOrder.Id).ToList().ToErrorOr<IReadOnlyList<StationOrder>>();
  }
}
