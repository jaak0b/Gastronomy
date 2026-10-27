using EntityFrameworkCore.Projectables;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Queries;

public static class OrderQueries
{
  [Projectable]
  public static IEnumerable<OrderItem> Items(this Order order) => order.StationOrders.SelectMany(stationOrder => stationOrder.Items);

  [Projectable]
  public static bool HasItemsToHandOut(this Order order) => order.Items().Any(item => item.IsOpen());

  [Projectable]
  public static bool IsPartlyHandedOut(this Order order) => order.HasItemsToHandOut() && order.Items().Any(item => item.IsHandedOut());

  [Projectable]
  public static bool HasItemsToSettle(this Order order) => order.Items().Any(item => item.IsToSettle());

  [Projectable]
  public static bool IsPartlySettled(this Order order) => order.HasItemsToSettle() && order.Items().Any(item => item.IsStillOrdered() && item.IsSettled());

  public static OrderStatus Status(this Order order)
  {
    if (!order.HasItemsToHandOut())
      return OrderStatus.Fulfilled;

    if (order.IsPartlyHandedOut())
      return OrderStatus.PartiallyFulfilled;

    return OrderStatus.Open;
  }
}
