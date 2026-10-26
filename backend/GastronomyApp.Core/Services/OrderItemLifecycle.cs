using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Mapping;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class OrderItemLifecycle
{
  public bool CanAppendStatus(OrderItem item, OrderItemState targetState)
  {
    ArgumentNullException.ThrowIfNull(item);

    return IsAllowed(CurrentStateOf(item), targetState);
  }

  public void AppendStatus(OrderItem item, OrderItemState targetState, OrderItemStatusAuthor author, DateTime changedAtUtc)
  {
    ArgumentNullException.ThrowIfNull(item);
    ArgumentNullException.ThrowIfNull(author);

    var currentState = CurrentStateOf(item);

    if (!IsAllowed(currentState, targetState))
      throw new InvalidOperationException($"Order item {item.Id} may not change from {currentState?.ToString() ?? "no status"} to {targetState}.");

    EnsureAuthorFits(currentState, author);

    OrderItemStatus status = new()
                             {
                               Id = Guid.NewGuid(),
                               OrderItemId = item.Id,
                               OrderItem = item,
                               PreviousStatusId = item.CurrentStatus?.Id,
                               PreviousStatus = item.CurrentStatus,
                               Status = targetState,
                               CreatedAtUtc = changedAtUtc,
                               CreatedByStaffMemberId = author.StaffMemberId,
                               CreatedByStationId = author.StationId
                             };

    item.Statuses.Add(status);

    if (currentState is not null)
      PointAt(item, status);
  }

  public void PointAtFirstStatus(OrderItem item)
  {
    ArgumentNullException.ThrowIfNull(item);

    if (item.CurrentStatusId is not null || item.CurrentStatus is not null || item.Statuses.Count != 1)
      throw new InvalidOperationException($"Order item {item.Id} has no single first status waiting to become its current one.");

    PointAt(item, item.Statuses[0]);
  }

  private void PointAt(OrderItem item, OrderItemStatus status)
  {
    item.CurrentStatus = status;
    item.CurrentStatusId = status.Id;
  }

  private OrderItemState? CurrentStateOf(OrderItem item)
  {
    if (item.CurrentStatus is not null || item.CurrentStatusId is not null)
      return OrderItemStates.CurrentStatusOf(item).Status;

    if (item.Statuses.Count > 0)
      throw new InvalidOperationException($"The first status of order item {item.Id} must be saved and made its current one before the item changes again.");

    return null;
  }

  private bool IsAllowed(OrderItemState? currentState, OrderItemState targetState)
  {
    return currentState switch
           {
             null => targetState == OrderItemState.Open,
             OrderItemState.Open => targetState == OrderItemState.HandedOut,
             OrderItemState.HandedOut => targetState == OrderItemState.Open,
             _ => false
           };
  }

  private void EnsureAuthorFits(OrderItemState? currentState, OrderItemStatusAuthor author)
  {
    var placedByWaiter = currentState is null;
    var namesWaiterOnly = author.StaffMemberId is not null && author.StationId is null;
    var namesStationOnly = author.StationId is not null && author.StaffMemberId is null;

    if (placedByWaiter && !namesWaiterOnly)
      throw new ArgumentException("The first status of an order item is set by the order's waiter alone.", nameof(author));

    if (!placedByWaiter && !namesStationOnly)
      throw new ArgumentException("A station hands an order item out and puts it back, so the station alone sets that status.", nameof(author));
  }
}
