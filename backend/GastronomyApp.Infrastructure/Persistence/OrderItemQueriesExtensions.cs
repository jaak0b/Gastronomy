using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.Persistence;

public static class OrderItemQueriesExtensions
{
  public static IQueryable<OrderItem> WhereNotHandedOut(this IQueryable<OrderItem> items)
  {
    ArgumentNullException.ThrowIfNull(items);

    return items.Where(item => item.FulfilledAtUtc == null);
  }

  public static IQueryable<OrderItem> WhereNotHandedOutOrNotSettled(this IQueryable<OrderItem> items)
  {
    ArgumentNullException.ThrowIfNull(items);

    return items.Where(item => item.FulfilledAtUtc == null || item.SettledAtUtc == null);
  }

  public static IQueryable<OrderItem> WhereFestivalIs(this IQueryable<OrderItem> items, Guid festivalId)
  {
    ArgumentNullException.ThrowIfNull(items);

    return items.Where(item => item.StationOrder.FestivalId == festivalId);
  }
}
