namespace GastronomyApp.Core.Entities;

public sealed class CatalogItem
{
  public required Guid Id { get; set; }
  public required string Name { get; set; }
  public required string CategoryName { get; set; }
  public required int PriceCents { get; set; }
  public required int SortOrder { get; set; }
  public required bool IsActive { get; set; }
  public required bool IsAvailable { get; set; }
}
