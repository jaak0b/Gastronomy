namespace GastronomyApp.Core.Results;

public sealed record RoutingFailure
{
  public required RoutingFailureReason Reason { get; init; }
}
