namespace GastronomyApp.Core.Requests;

public sealed record SaveStationDetailsRequest
{
  public required string? Name { get; init; }

  public required int SortOrder { get; init; }
}
