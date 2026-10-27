using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class OrderService
{
  public OrderStatus StatusOf(Order order)
  {
    ArgumentNullException.ThrowIfNull(order);

    List<OrderItemProgress> items = order.StationOrders.SelectMany(stationOrder => stationOrder.Items).Select(item => new OrderItemProgress(item.State(), item.SettledAtUtc != null)).ToList();
    var handOutProgress = HandOutProgressOf(items);

    if (handOutProgress == ShareState.None)
      return OrderStatus.Open;

    if (handOutProgress == ShareState.All)
      return OrderStatus.Fulfilled;

    return OrderStatus.PartiallyFulfilled;
  }

  public ShareState HandOutProgressOf(IEnumerable<OrderItemProgress> orderItems)
  {
    ArgumentNullException.ThrowIfNull(orderItems);

    return ShareOfFlaggedItems(orderItems.Where(item => !item.State.IsCancelled()).Select(item => item.State.IsHandedOut()));
  }

  public ShareState SettlementProgressOf(IEnumerable<OrderItemProgress> orderItems)
  {
    ArgumentNullException.ThrowIfNull(orderItems);

    return ShareOfFlaggedItems(orderItems.Where(item => !item.State.IsCancelled()).Select(item => item.IsSettled));
  }

  public ShareState ShareOfFlaggedItems(IEnumerable<bool> itemFlags)
  {
    ArgumentNullException.ThrowIfNull(itemFlags);

    List<bool> flags = itemFlags.ToList();

    var flaggedItemCount = flags.Count(isFlagged => isFlagged);

    if (flaggedItemCount == flags.Count)
      return ShareState.All;

    if (flaggedItemCount == 0)
      return ShareState.None;

    return ShareState.Some;
  }

  public int TotalCentsOf(Order order)
  {
    ArgumentNullException.ThrowIfNull(order);

    return order.StationOrders.SelectMany(stationOrder => stationOrder.Items).Where(item => !item.IsCancelled()).Sum(item => item.UnitPriceCents);
  }
}
