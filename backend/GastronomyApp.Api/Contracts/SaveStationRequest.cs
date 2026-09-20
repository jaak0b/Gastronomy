namespace GastronomyApp.Api.Contracts;

public sealed record SaveStationRequest
{
  public required string? Name { get; init; }

  public required int SortOrder { get; init; }
}
