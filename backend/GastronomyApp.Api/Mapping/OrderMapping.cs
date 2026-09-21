using GastronomyApp.Contracts;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class OrderMapping : IRegister
{
  private readonly OrderStatusCalculator _statusCalculator = new();
  private readonly OrderTotalCalculator _totalCalculator = new();

  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<StationOrder, StationOrderView>()
          .Map(view => view.StationOrderId, stationOrder => stationOrder.Id)
          .Map(view => view.StationName, stationOrder => stationOrder.Station.Name)
          .Map(view => view.ItemIds, stationOrder => stationOrder.Items.OrderBy(item => item.ItemName).ThenBy(item => item.Note).ThenBy(item => item.Id).Select(item => item.Id).ToList());

    config.NewConfig<Order, PlacedOrderView>()
          .Map(view => view.OrderId, order => order.Id)
          .Map(view => view.Status, order => _statusCalculator.Calculate(order))
          .Map(view => view.TotalCents, order => _totalCalculator.SumTotalCents(order))
          .Map(view => view.StationOrders, order => order.StationOrders.OrderBy(stationOrder => stationOrder.Station.SortOrder).ThenBy(stationOrder => stationOrder.Station.Name).ThenBy(stationOrder => stationOrder.Id).ToList());

    config.NewConfig<Order, OrderStatusChangedEvent>().Map(payload => payload.OrderId, order => order.Id).Map(payload => payload.Status, order => _statusCalculator.Calculate(order));
  }
}
