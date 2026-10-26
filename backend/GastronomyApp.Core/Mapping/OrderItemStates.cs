using System.Linq.Expressions;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Mapping;

public static class OrderItemStates
{
  public static Expression<Func<OrderItem, bool>> HandedOutPredicate { get; } = item => item.CurrentStatus!.Status == OrderItemState.HandedOut;

  public static Expression<Func<OrderItem, bool>> NotHandedOutPredicate { get; } = item => item.CurrentStatus!.Status != OrderItemState.HandedOut;

  public static Expression<Func<OrderItem, bool>> NotHandedOutOrNotSettledPredicate { get; } = item => item.CurrentStatus!.Status != OrderItemState.HandedOut || item.SettledAtUtc == null;

  public static OrderItemStatus CurrentStatusOf(OrderItem item)
  {
    ArgumentNullException.ThrowIfNull(item);

    if (item.CurrentStatus is null)
      throw new InvalidOperationException($"Order item {item.Id} was read without its current status, so the query that loaded it must include OrderItem.CurrentStatus.");

    return item.CurrentStatus;
  }

  public static bool IsHandedOut(OrderItem item)
  {
    return CurrentStatusOf(item).Status == OrderItemState.HandedOut;
  }

  public static DateTime? HandedOutAtUtcOf(OrderItem item)
  {
    var currentStatus = CurrentStatusOf(item);

    if (currentStatus.Status == OrderItemState.HandedOut)
      return currentStatus.CreatedAtUtc;

    return null;
  }
}
