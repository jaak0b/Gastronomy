namespace GastronomyApp.Core.Services;

public sealed record SaveStationNameRequest
{
  public required string? Name { get; init; }

  public required int SortOrder { get; init; }
}
