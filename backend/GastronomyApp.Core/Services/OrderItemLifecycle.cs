using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class OrderItemLifecycle
{
  public OrderItemChangeVerdict JudgeChange(OrderItem item, OrderItemState targetState)
  {
    return new StateChange(CurrentStateOf(item), targetState) switch
           {
             { From: null, To: OrderItemState.Open } => OrderItemChangeVerdict.Allowed,
             { From: null, To: not OrderItemState.Open } => OrderItemChangeVerdict.ItemIsNotPlacedYet,
             { From: OrderItemState.Open, To: OrderItemState.Open } => OrderItemChangeVerdict.ItemIsNotHandedOut,
             { From: OrderItemState.Open, To: OrderItemState.HandedOut } => OrderItemChangeVerdict.Allowed,
             { From: OrderItemState.Open, To: OrderItemState.Cancelled } => OrderItemChangeVerdict.Allowed,
             { From: OrderItemState.Open, To: OrderItemState.CancellationAcknowledged } => OrderItemChangeVerdict.ItemDoesNotAwaitCancellationAcknowledgement,
             { From: OrderItemState.HandedOut, To: OrderItemState.Open } => OrderItemChangeVerdict.Allowed,
             { From: OrderItemState.HandedOut, To: OrderItemState.HandedOut } => OrderItemChangeVerdict.AlreadyInThatState,
             { From: OrderItemState.HandedOut, To: OrderItemState.Cancelled } => OrderItemChangeVerdict.Allowed,
             { From: OrderItemState.HandedOut, To: OrderItemState.CancellationAcknowledged } => OrderItemChangeVerdict.ItemDoesNotAwaitCancellationAcknowledgement,
             { From: OrderItemState.Cancelled, To: OrderItemState.Open or OrderItemState.HandedOut } => OrderItemChangeVerdict.ItemIsCancelled,
             { From: OrderItemState.Cancelled, To: OrderItemState.Cancelled } => OrderItemChangeVerdict.AlreadyInThatState,
             { From: OrderItemState.Cancelled, To: OrderItemState.CancellationAcknowledged } => JudgeAcknowledgementOf(item),
             { From: OrderItemState.CancellationAcknowledged, To: OrderItemState.Open or OrderItemState.HandedOut or OrderItemState.Cancelled } => OrderItemChangeVerdict.ItemIsCancelled,
             { From: OrderItemState.CancellationAcknowledged, To: OrderItemState.CancellationAcknowledged } => OrderItemChangeVerdict.AlreadyInThatState,
             _ => throw new ArgumentOutOfRangeException(nameof(targetState), targetState, $"Order item {item.Id} is in a state or asked for a state the lifecycle does not know.")
           };
  }

  public void AppendStatus(OrderItem item, OrderItemState targetState, OrderItemStatusAuthor author, DateTime changedAtUtc)
  {
    var currentState = CurrentStateOf(item);
    var verdict = JudgeChange(item, targetState);

    if (verdict != OrderItemChangeVerdict.Allowed)
      throw new InvalidOperationException($"Order item {item.Id} may not change from {currentState?.ToString() ?? "no status"} to {targetState}: {verdict}.");

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

  private OrderItemChangeVerdict JudgeAcknowledgementOf(OrderItem item)
  {
    if (item.CurrentStatus!.PreviousStatus is null)
      throw new InvalidOperationException($"Order item {item.Id} was read without the status before its current one, so the query that loaded it must include OrderItemStatus.PreviousStatus.");

    if (item.AwaitsCancellationAcknowledgement())
      return OrderItemChangeVerdict.Allowed;

    return OrderItemChangeVerdict.ItemDoesNotAwaitCancellationAcknowledgement;
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

  private readonly record struct StateChange(OrderItemState? From, OrderItemState To);
}
