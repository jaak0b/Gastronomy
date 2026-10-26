using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using Mapster;

namespace GastronomyApp.Core.Mapping;

public sealed class OrderMapping : IRegister
{
  private readonly OrderService _orderService = new();

  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<StationOrder, StationOrderView>()
          .Map(view => view.StationOrderId, stationOrder => stationOrder.Id)
          .Map(view => view.StationName, stationOrder => stationOrder.Station.Name)
          .Map(view => view.ItemIds, stationOrder => stationOrder.Items.OrderBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Note, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).Select(item => item.Id).ToList());

    config.NewConfig<Order, PlacedOrderView>()
          .Map(view => view.OrderId, order => order.Id)
          .Map(view => view.Status, order => _orderService.StatusOf(order))
          .Map(view => view.TotalCents, order => _orderService.TotalCentsOf(order))
          .Map(view => view.StationOrders, order => order.StationOrders.OrderBy(stationOrder => stationOrder.Station.SortOrder).ThenBy(stationOrder => stationOrder.Station.Name, StringComparer.OrdinalIgnoreCase).ThenBy(stationOrder => stationOrder.Id).ToList());
  }
}
