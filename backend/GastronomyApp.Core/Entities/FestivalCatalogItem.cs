namespace GastronomyApp.Core.Entities;

public sealed class FestivalCatalogItem
{
  public required Guid Id { get; set; }

  public required Guid FestivalId { get; set; }

  public required Guid CatalogItemId { get; set; }

  public required int PriceCents { get; set; }

  public required bool IsAvailable { get; set; }
}
