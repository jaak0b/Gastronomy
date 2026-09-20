namespace GastronomyApp.Core.Results;

public sealed record StationAdministrationFailure
{
  public required StationAdministrationFailureReason Reason { get; init; }

  public int StrandedItemCount { get; init; }
}
