namespace GastronomyApp.Core.Results;

public sealed record FestivalStationFailure
{
  public required FestivalStationFailureReason Reason { get; init; }

  public int StrandedItemCount { get; init; }
}
