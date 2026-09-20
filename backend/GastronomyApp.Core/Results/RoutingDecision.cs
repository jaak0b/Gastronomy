namespace GastronomyApp.Core.Results;

public sealed record RoutingDecision
{
  public required Guid ResolvedStationId { get; init; }
}
