using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed class IngredientService
{
  public IReadOnlyList<string> ActiveItemNamesOf(Ingredient ingredient)
  {
    ArgumentNullException.ThrowIfNull(ingredient);

    return ingredient.CatalogItems.Where(recipeLine => recipeLine.CatalogItem.IsActive).Select(recipeLine => recipeLine.CatalogItem.Name).OrderBy(itemName => itemName, StringComparer.Ordinal).ToList();
  }
}
