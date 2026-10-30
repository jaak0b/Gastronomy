using EntityFrameworkCore.Projectables;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Queries;

public static class IngredientQueries
{
  [Projectable]
  public static IEnumerable<string> UsedByItemNames(this Ingredient ingredient) => ingredient.CatalogItems.Select(recipeLine => recipeLine.CatalogItem.Name).OrderBy(itemName => itemName);
}
