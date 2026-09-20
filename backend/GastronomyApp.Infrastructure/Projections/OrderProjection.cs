using GastronomyApp.Core.Entities;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Infrastructure.QueryRows;
using Mapster;

namespace GastronomyApp.Infrastructure.Projections;

public sealed class OrderProjection : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<OrderItem, PlacedOrderItem>().Map(placed => placed.OrderItemId, item => item.Id).Map(placed => placed.IsFulfilled, item => item.FulfilledAtUtc != null);

    config.NewConfig<PlacedStationOrderRow, PlacedStationOrder>()
          .Map(placed => placed.StationOrderId, row => row.StationOrder.Id)
          .Map(placed => placed.StationId, row => row.StationOrder.StationId)
          .Map(placed => placed.StationName, row => row.Station.Name)
          .Map(placed => placed.StationOrderNumber, row => row.StationOrder.StationOrderNumber)
          .Map(placed => placed.DeliveryMode, row => row.StationOrder.DeliveryMode)
          .Map(placed => placed.Items, row => row.StationOrder.Items.OrderBy(item => item.ItemName).ThenBy(item => item.Note).ThenBy(item => item.Id));
  }
}
