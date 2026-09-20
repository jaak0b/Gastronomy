using GastronomyApp.Core.Entities;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Infrastructure.QueryRows;
using Mapster;

namespace GastronomyApp.Infrastructure.Projections;

public sealed class StationOrderProjection : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<OrderItem, QueuedOrderItem>().Map(queued => queued.OrderItemId, item => item.Id);

    config.NewConfig<CatalogItem, QueuedWork>();

    config.NewConfig<Order, OrderFulfillmentCounts>()
          .Map(counts => counts.OrderId, order => order.Id)
          .Map(counts => counts.ItemCount, order => order.StationOrders.SelectMany(stationOrder => stationOrder.Items).Count())
          .Map(counts => counts.FulfilledItemCount, order => order.StationOrders.SelectMany(stationOrder => stationOrder.Items).Count(item => item.FulfilledAtUtc != null));

    config.NewConfig<StationQueuedWorkRow, StationQueuedWork>().Map(work => work.StationId, row => row.StationOrder.StationId).Map(work => work.Work, row => row.CatalogItem);

    config.NewConfig<QueuedStationOrderRow, QueuedStationOrder>()
          .Map(queued => queued.StationOrderId, row => row.StationOrder.Id)
          .Map(queued => queued.GlobalOrderNumber, row => row.Order.GlobalOrderNumber)
          .Map(queued => queued.StationOrderNumber, row => row.StationOrder.StationOrderNumber)
          .Map(queued => queued.TableName, row => row.Order.TableName)
          .Map(queued => queued.DeliveryMode, row => row.StationOrder.DeliveryMode)
          .Map(queued => queued.CreatedAtUtc, row => row.Order.CreatedAtUtc)
          .Map(queued => queued.IsHiddenFromAsItComesQueue, row => row.StationOrder.IsHiddenFromAsItComesQueue)
          .Map(queued => queued.ItemCount, row => row.StationOrder.Items.Count)
          .Map(queued => queued.FulfilledItemCount, row => row.StationOrder.Items.Count(item => item.FulfilledAtUtc != null))
          .Map(queued => queued.Items, row => row.StationOrder.Items.OrderBy(item => item.ItemName).ThenBy(item => item.Note).ThenBy(item => item.Id));
  }
}
