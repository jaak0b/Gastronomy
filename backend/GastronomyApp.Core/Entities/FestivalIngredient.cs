using GastronomyApp.Core.Announcements;

namespace GastronomyApp.Core.Entities;

[Raises(HubEvent.ConfigurationChanged)]
public sealed class FestivalIngredient
{
  public required Guid Id { get; set; }

  public required Guid FestivalId { get; set; }

  public required Guid IngredientId { get; set; }

  public double? AvailableAmount { get; set; }

  public Festival Festival { get; set; } = null!;

  public Ingredient Ingredient { get; set; } = null!;
}
