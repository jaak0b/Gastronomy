using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Queries;

public static class OrderItemQueries
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

  public static IQueryable<ConsumedIngredient> ConsumedIngredients(this IQueryable<OrderItem> items, IQueryable<CatalogItemIngredient> recipeLines)
  {
    ArgumentNullException.ThrowIfNull(items);
    ArgumentNullException.ThrowIfNull(recipeLines);

    return items.Join(recipeLines,
                      item => item.CatalogItemId,
                      recipeLine => recipeLine.CatalogItemId,
                      (item, recipeLine) => new ConsumedIngredient
                                            {
                                              IngredientId = recipeLine.IngredientId,
                                              OrderedAtUtc = item.StationOrder.Order.CreatedAtUtc,
                                              Amount = recipeLine.Amount
                                            });
  }
}
