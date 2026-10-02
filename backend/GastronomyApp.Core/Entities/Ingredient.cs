using System.Collections.ObjectModel;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Announcements;

namespace GastronomyApp.Core.Entities;

[Raises(HubEvent.ConfigurationChanged)]
public sealed class Ingredient
{
  public required Guid Id { get; set; }

  public required string Name { get; set; }

  public required IngredientUnit Unit { get; set; }

  public required bool IsActive { get; set; }

  public Collection<FestivalIngredient> FestivalIngredients { get; } = [];

  public Collection<CatalogItemIngredient> CatalogItems { get; } = [];
}
