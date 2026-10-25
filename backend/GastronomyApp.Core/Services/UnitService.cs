using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed class UnitService
{
  public IReadOnlyList<string> IngredientNamesOf(Unit unit)
  {
    ArgumentNullException.ThrowIfNull(unit);

    return unit.Ingredients.Select(ingredient => ingredient.Name).OrderBy(name => name, StringComparer.Ordinal).ToList();
  }
}
