namespace GastronomyApp.Core.Services;

public sealed record StationOrderVisibilityFailure
{
  public required StationOrderVisibilityFailureReason Reason { get; init; }
}
