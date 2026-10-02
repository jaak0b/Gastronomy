using GastronomyApp.Core.Announcements;

namespace GastronomyApp.Core.Entities;

[Raises(HubEvent.ConfigurationChanged)]
public sealed class CatalogItemIngredient
{
  public required Guid Id { get; set; }

  public required Guid CatalogItemId { get; set; }

  public required Guid IngredientId { get; set; }

  public required double Amount { get; set; }

  public CatalogItem CatalogItem { get; set; } = null!;

  public Ingredient Ingredient { get; set; } = null!;
}
