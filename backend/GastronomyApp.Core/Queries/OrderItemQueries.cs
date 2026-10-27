using EntityFrameworkCore.Projectables;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Queries;

public static class OrderItemQueries
{
  public static IQueryable<OrderItem> WhereOpen(this IQueryable<OrderItem> items)
  {
    return items.Where(item => item.IsOpen());
  }

  public static IQueryable<OrderItem> WhereStillOrdered(this IQueryable<OrderItem> items)
  {
    return items.Where(item => item.IsStillOrdered());
  }

  public static IQueryable<OrderItem> WhereToHandOutOrToSettle(this IQueryable<OrderItem> items)
  {
    return items.Where(item => item.IsToHandOutOrToSettle());
  }

  public static IQueryable<OrderItem> WhereFestivalIs(this IQueryable<OrderItem> items, Guid festivalId)
  {
    return items.Where(item => item.StationOrder.FestivalId == festivalId);
  }

  public static IQueryable<ConsumedIngredient> ConsumedIngredients(this IQueryable<OrderItem> items, IQueryable<CatalogItemIngredient> recipeLines)
  {
    return items.WhereStillOrdered()
   .Join(recipeLines,
         item => item.CatalogItemId,
         recipeLine => recipeLine.CatalogItemId,
         (item, recipeLine) => new ConsumedIngredient
                               {
                                 IngredientId = recipeLine.IngredientId,
                                 OrderedAtUtc = item.StationOrder.Order.CreatedAtUtc,
                                 Amount = recipeLine.Amount
                               });
  }

  [Projectable]
  public static bool IsOpen(this OrderItem item) => item.CurrentStatus!.Status.IsOpen();

  [Projectable]
  public static bool IsHandedOut(this OrderItem item) => item.CurrentStatus!.Status.IsHandedOut();

  [Projectable]
  public static bool IsCancelled(this OrderItem item) => item.CurrentStatus!.Status.IsCancelled();

  [Projectable]
  public static bool IsStillOrdered(this OrderItem item) => !item.IsCancelled();

  [Projectable]
  public static bool AwaitsCancellationAcknowledgement(this OrderItem item) => item.CurrentStatus!.Status == OrderItemState.Cancelled && item.CurrentStatus!.PreviousStatus!.Status.IsOpen();

  [Projectable]
  public static bool IsOpenOrAwaitsCancellationAcknowledgement(this OrderItem item) => item.IsOpen() || item.AwaitsCancellationAcknowledgement();

  [Projectable]
  public static bool IsOnTheQueueCard(this OrderItem item) => item.IsOpenOrAwaitsCancellationAcknowledgement() || item.IsHandedOut();

  [Projectable]
  public static bool IsSettled(this OrderItem item) => item.SettledAtUtc != null;

  [Projectable]
  public static bool IsToHandOutOrToSettle(this OrderItem item) => item.IsOpen() || (item.IsHandedOut() && !item.IsSettled());

  [Projectable]
  public static bool IsToSettle(this OrderItem item) => item.IsStillOrdered() && !item.IsSettled();

  [Projectable]
  public static bool MatchesTableSearch(this OrderItem item, PaymentStatus? paymentStatus, bool? includeCancelled) =>
    (paymentStatus == null || (paymentStatus == PaymentStatus.Paid && item.IsSettled()) || (paymentStatus == PaymentStatus.NotPaid && !item.IsSettled())) && ((includeCancelled ?? true) || item.IsStillOrdered());

  public static bool WasHandedOutWhenCancelled(this OrderItem item)
  {
    ArgumentNullException.ThrowIfNull(item);

    return item.State() switch
           {
             OrderItemState.Cancelled => StatusBefore(item.CurrentStatus!).Status.IsHandedOut(),
             _ => false
           };
  }

  private static OrderItemStatus StatusBefore(OrderItemStatus status)
  {
    return status.PreviousStatus ?? throw new InvalidOperationException($"Status {status.Id} of order item {status.OrderItemId} was read without the status before it, so the query that loaded it must include OrderItemStatus.PreviousStatus.");
  }

  [Projectable]
  public static OrderItemState State(this OrderItem item) => item.CurrentStatus!.Status;

  [Projectable]
  public static DateTime StateSetAtUtc(this OrderItem item) => item.CurrentStatus!.CreatedAtUtc;
}
