using GastronomyApp.Contracts.Stations;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class StationQueueMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<OrderItem, StationQueueItemView>().Map(view => view.OrderItemId, item => item.Id);

    config.NewConfig<StationOrder, StationOrderQueueView>()
          .Map(view => view.StationOrderId, stationOrder => stationOrder.Id)
          .Map(view => view.GlobalOrderNumber, stationOrder => stationOrder.Order.GlobalOrderNumber)
          .Map(view => view.TableName, stationOrder => stationOrder.Order.TableName)
          .Map(view => view.StaffMemberName, stationOrder => stationOrder.Order.StaffMember.Name)
          .Map(view => view.CreatedAtUtc, stationOrder => stationOrder.Order.CreatedAtUtc)
          .Map(view => view.ItemCount, stationOrder => stationOrder.Items.Count)
          .Map(view => view.FulfilledItemCount, stationOrder => stationOrder.Items.Count(item => item.FulfilledAtUtc != null))
          .Map(view => view.Items, stationOrder => stationOrder.Items.OrderBy(item => item.ItemName).ThenBy(item => item.Note).ThenBy(item => item.Id).ToList());

    config.NewConfig<Station, StationSummaryView>();

    config.NewConfig<Station, StationQueueView>().Map(view => view.Station, station => station).Map(view => view.Orders, station => OrdersInTheOpenColumn(station)).Map(view => view.AsItComes, station => OrdersInTheAsItComesColumn(station));
  }

  private IReadOnlyList<StationOrder> OrdersInTheOpenColumn(Station station)
  {
    return InStationOrder(station.StationOrders);
  }

  private IReadOnlyList<StationOrder> OrdersInTheAsItComesColumn(Station station)
  {
    return InStationOrder(station.StationOrders.Where(stationOrder => stationOrder.IsInAsItComesColumn()));
  }

  private IReadOnlyList<StationOrder> InStationOrder(IEnumerable<StationOrder> stationOrders)
  {
    return stationOrders.OrderBy(stationOrder => stationOrder.StationOrderNumber).ToList();
  }
}
