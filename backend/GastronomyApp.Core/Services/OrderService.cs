using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Mapping;

namespace GastronomyApp.Core.Services;

public sealed class OrderService
{
  public OrderStatus StatusOf(Order order)
  {
    ArgumentNullException.ThrowIfNull(order);

    var handOutShare = ShareOfFlaggedItems(order.StationOrders.SelectMany(stationOrder => stationOrder.Items).Select(OrderItemStates.IsHandedOut));

    if (handOutShare == ShareState.None)
      return OrderStatus.Open;

    if (handOutShare == ShareState.All)
      return OrderStatus.Fulfilled;

    return OrderStatus.PartiallyFulfilled;
  }

  public ShareState ShareOfFlaggedItems(IEnumerable<bool> itemFlags)
  {
    ArgumentNullException.ThrowIfNull(itemFlags);

    List<bool> flags = itemFlags.ToList();

    var flaggedItemCount = flags.Count(isFlagged => isFlagged);

    if (flaggedItemCount == 0)
      return ShareState.None;

    if (flaggedItemCount == flags.Count)
      return ShareState.All;

    return ShareState.Some;
  }

  public int TotalCentsOf(Order order)
  {
    ArgumentNullException.ThrowIfNull(order);

    return order.StationOrders.SelectMany(stationOrder => stationOrder.Items).Sum(item => item.UnitPriceCents);
  }
}
