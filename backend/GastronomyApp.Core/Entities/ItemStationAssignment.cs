namespace GastronomyApp.Core.Entities;

public sealed class ItemStationAssignment
{
  public required Guid Id { get; set; }

  public required Guid FestivalId { get; set; }

  public required Guid CatalogItemId { get; set; }

  public required Guid StationId { get; set; }

  public Festival Festival { get; set; } = null!;

  public CatalogItem CatalogItem { get; set; } = null!;

  public Station Station { get; set; } = null!;
}
