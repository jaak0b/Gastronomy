using GastronomyApp.Contracts;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class OpenItemMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<OrderItem, OpenOrderItemView>()
          .Map(view => view.OrderItemId, item => item.Id)
          .Map(view => view.OrderId, item => item.StationOrder.Order.Id)
          .Map(view => view.GlobalOrderNumber, item => item.StationOrder.Order.GlobalOrderNumber)
          .Map(view => view.OrderedAtUtc, item => item.StationOrder.Order.CreatedAtUtc);

    config.NewConfig<OrderItem, TableOrderRecordItemView>()
          .Map(view => view.OrderItemId, item => item.Id)
          .Map(view => view.OrderId, item => item.StationOrder.Order.Id)
          .Map(view => view.GlobalOrderNumber, item => item.StationOrder.Order.GlobalOrderNumber)
          .Map(view => view.OrderedAtUtc, item => item.StationOrder.Order.CreatedAtUtc);

    config.NewConfig<Order, TableOrderRecordView>().Map(view => view.OrderId, order => order.Id).Map(view => view.StaffMemberName, order => order.StaffMember.Name).Map(view => view.Items, order => PositionsOfTheOrder(order));
  }

  private IReadOnlyList<OrderItem> PositionsOfTheOrder(Order order)
  {
    return order.StationOrders.SelectMany(stationOrder => stationOrder.Items).OrderBy(item => item.ItemName, StringComparer.Ordinal).ThenBy(item => item.Note, StringComparer.Ordinal).ThenBy(item => item.Id).ToList();
  }
}
