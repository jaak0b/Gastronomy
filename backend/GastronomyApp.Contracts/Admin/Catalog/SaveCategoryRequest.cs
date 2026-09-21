namespace GastronomyApp.Contracts.Admin.Catalog;

public sealed record SaveCategoryRequest
{
  public required string? Name { get; init; }

  public required string? ColourHex { get; init; }
}
