namespace GastronomyApp.Core.Entities;

public sealed class CatalogCategory
{
  public required Guid Id { get; set; }

  public required string Name { get; set; }

  public required string NormalizedName { get; set; }

  public required string ColourHex { get; set; }

  public required int SortOrder { get; set; }

  public required bool IsActive { get; set; }
}
