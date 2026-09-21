namespace GastronomyApp.Contracts;

public sealed record SaveCategoryRequest
{
  public required string? Name { get; init; }

  public required string? ColourHex { get; init; }
}
