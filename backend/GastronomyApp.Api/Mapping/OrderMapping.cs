using GastronomyApp.Contracts.Events;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class OrderMapping
{
  private readonly OrderService _orderService;

  public OrderMapping(OrderService orderService)
  {
    _orderService = orderService;
  }

  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<StationOrder, StationOrderView>()
          .Map(view => view.StationOrderId, stationOrder => stationOrder.Id)
          .Map(view => view.StationName, stationOrder => stationOrder.Station.Name)
          .Map(view => view.ItemIds, stationOrder => stationOrder.Items.OrderBy(item => item.ItemName).ThenBy(item => item.Note).ThenBy(item => item.Id).Select(item => item.Id).ToList());

    config.NewConfig<Order, PlacedOrderView>()
          .Map(view => view.OrderId, order => order.Id)
          .Map(view => view.Status, order => _orderService.StatusOf(order))
          .Map(view => view.TotalCents, order => _orderService.TotalCentsOf(order))
          .Map(view => view.StationOrders, order => order.StationOrders.OrderBy(stationOrder => stationOrder.Station.SortOrder).ThenBy(stationOrder => stationOrder.Station.Name).ThenBy(stationOrder => stationOrder.Id).ToList());

    config.NewConfig<Order, OrderStatusChangedEvent>().Map(payload => payload.OrderId, order => order.Id).Map(payload => payload.Status, order => _orderService.StatusOf(order));
  }
}
