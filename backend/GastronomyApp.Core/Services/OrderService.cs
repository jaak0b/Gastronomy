using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed class OrderService
{
  public OrderStatus StatusOf(Order order)
  {
    ArgumentNullException.ThrowIfNull(order);

    List<OrderItem> items = order.StationOrders.SelectMany(stationOrder => stationOrder.Items).ToList();

    var fulfilledItemCount = items.Count(item => item.FulfilledAtUtc is not null);

    if (fulfilledItemCount == 0)
      return OrderStatus.Open;

    if (fulfilledItemCount >= items.Count)
      return OrderStatus.Fulfilled;

    return OrderStatus.PartiallyFulfilled;
  }

  public int TotalCentsOf(Order order)
  {
    ArgumentNullException.ThrowIfNull(order);

    return order.StationOrders.SelectMany(stationOrder => stationOrder.Items).Sum(item => item.UnitPriceCents);
  }
}
