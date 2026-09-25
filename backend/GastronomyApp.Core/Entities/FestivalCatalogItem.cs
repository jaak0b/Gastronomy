using GastronomyApp.Core.Announcements;

namespace GastronomyApp.Core.Entities;

[Raises(HubEvent.ConfigurationChanged)]
public sealed class FestivalCatalogItem
{
  public required Guid Id { get; set; }

  public required Guid FestivalId { get; set; }

  public required Guid CatalogItemId { get; set; }

  public required int PriceCents { get; set; }

  public required bool IsAvailable { get; set; }

  public Festival Festival { get; set; } = null!;

  public CatalogItem CatalogItem { get; set; } = null!;
}
