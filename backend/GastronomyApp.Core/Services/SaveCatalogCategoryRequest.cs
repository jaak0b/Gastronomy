namespace GastronomyApp.Core.Services;

public sealed record SaveCatalogCategoryRequest
{
  public required string? Name { get; init; }

  public required string? ColourHex { get; init; }
}
