using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Core.Mapping;

public sealed class OrderMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<StationOrder, StationOrderView>()
          .Map(view => view.StationOrderId, stationOrder => stationOrder.Id)
          .Map(view => view.StationName, stationOrder => stationOrder.Station.Name)
          .Map(view => view.ItemIds, stationOrder => stationOrder.Items.OrderBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Note, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).Select(item => item.Id).ToList());

    config.NewConfig<Order, PlacedOrderView>()
          .Map(view => view.OrderId, order => order.Id)
          .Map(view => view.Status, order => OrderStatus.Open)
          .Map(view => view.TotalCents, order => 0)
          .Map(view => view.StationOrders, order => order.StationOrders.OrderBy(stationOrder => stationOrder.Station.SortOrder).ThenBy(stationOrder => stationOrder.Station.Name, StringComparer.OrdinalIgnoreCase).ThenBy(stationOrder => stationOrder.Id).ToList());
  }
}
