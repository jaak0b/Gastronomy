using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class OrderItemLifecycle
{
  public bool CanAppendStatus(OrderItem item, OrderItemState targetState)
  {
    return CheckStatusChange(item, targetState) == OrderItemStatusChange.Allowed;
  }

  public OrderItemStatusChange CheckStatusChange(OrderItem item, OrderItemState targetState)
  {
    ArgumentNullException.ThrowIfNull(item);

    var currentState = CurrentStateOf(item);

    if (IsAllowed(item, currentState, targetState))
      return OrderItemStatusChange.Allowed;

    if (currentState == targetState)
      return OrderItemStatusChange.AlreadyInThatState;

    if (currentState is { } state && state.IsCancelled() && targetState != OrderItemState.CancellationAcknowledged)
      return OrderItemStatusChange.ItemIsCancelled;

    return OrderItemStatusChange.NotAllowed;
  }

  public void AppendStatus(OrderItem item, OrderItemState targetState, OrderItemStatusAuthor author, DateTime changedAtUtc)
  {
    ArgumentNullException.ThrowIfNull(item);
    ArgumentNullException.ThrowIfNull(author);

    var currentState = CurrentStateOf(item);

    if (!IsAllowed(item, currentState, targetState))
      throw new InvalidOperationException($"Order item {item.Id} may not change from {currentState?.ToString() ?? "no status"} to {targetState}.");

    EnsureAuthorFits(currentState, targetState, author);

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
    if (item.CurrentStatus is not null)
      return item.CurrentStatus.Status;

    if (item.Statuses.Count > 0)
      throw new InvalidOperationException($"The first status of order item {item.Id} must be saved and made its current one before the item changes again.");

    return null;
  }

  private bool IsAllowed(OrderItem item, OrderItemState? currentState, OrderItemState targetState)
  {
    return currentState switch
           {
             null => targetState == OrderItemState.Open,
             OrderItemState.Open => targetState is OrderItemState.HandedOut or OrderItemState.Cancelled,
             OrderItemState.HandedOut => targetState is OrderItemState.Open or OrderItemState.Cancelled,
             OrderItemState.Cancelled => targetState == OrderItemState.CancellationAcknowledged && AwaitsAcknowledgementWithItsPreviousStatusLoaded(item),
             _ => false
           };
  }

  private bool AwaitsAcknowledgementWithItsPreviousStatusLoaded(OrderItem item)
  {
    if (item.CurrentStatus!.PreviousStatus is null)
      throw new InvalidOperationException($"Order item {item.Id} was read without the status before its current one, so the query that loaded it must include OrderItemStatus.PreviousStatus.");

    return item.AwaitsCancellationAcknowledgement();
  }

  private void EnsureAuthorFits(OrderItemState? currentState, OrderItemState targetState, OrderItemStatusAuthor author)
  {
    var setByWaiter = currentState is null || targetState == OrderItemState.Cancelled;
    var namesWaiterOnly = author.StaffMemberId is not null && author.StationId is null;
    var namesStationOnly = author.StationId is not null && author.StaffMemberId is null;

    if (setByWaiter && !namesWaiterOnly)
      throw new ArgumentException("A waiter places an order item and cancels it, so the waiter alone sets that status.", nameof(author));

    if (!setByWaiter && !namesStationOnly)
      throw new ArgumentException("A station hands an order item out, puts it back and acknowledges its cancellation, so the station alone sets that status.", nameof(author));
  }
}
