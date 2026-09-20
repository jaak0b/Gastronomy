using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Api.Endpoints;

public sealed class StationQueueViewBuilder
{
  public StationQueueView Build(StationQueue queue)
  {
    ArgumentNullException.ThrowIfNull(queue);

    return new(new(queue.StationId, queue.StationName),
               BuildStationOrders(queue.Orders),
               BuildStationOrders(queue.AsItComesOrders));
  }

  public IReadOnlyList<StationOrderQueueView> BuildStationOrders(IReadOnlyCollection<QueuedStationOrder> stationOrders)
  {
    ArgumentNullException.ThrowIfNull(stationOrders);

    return [.. stationOrders.Select(BuildStationOrder)];
  }

  private StationOrderQueueView BuildStationOrder(QueuedStationOrder stationOrder)
  {
    return new(stationOrder.StationOrderId,
               stationOrder.GlobalOrderNumber,
               stationOrder.StationOrderNumber,
               stationOrder.TableName,
               stationOrder.Note,
               stationOrder.DeliveryMode,
               stationOrder.CreatedAtUtc,
               stationOrder.IsHiddenFromAsItComesQueue,
               stationOrder.ItemCount,
               stationOrder.FulfilledItemCount,
               [
                 .. stationOrder.Items.Select(item => new StationQueueItemView(item.OrderItemId,
                                                                               item.ItemName,
                                                                               item.Note,
                                                                               item.FulfilledAtUtc))
               ]);
  }
}
