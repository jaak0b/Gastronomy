using EntityFrameworkCore.Projectables;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Queries;

public static class UnitQueries
{
  [Projectable]
  public static IEnumerable<string> UsedByIngredientNames(this Unit unit) => unit.Ingredients.Select(ingredient => ingredient.Name).OrderBy(ingredientName => ingredientName);
}
