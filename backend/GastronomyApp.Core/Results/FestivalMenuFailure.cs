namespace GastronomyApp.Core.Results;

public sealed record FestivalMenuFailure
{
  public required FestivalMenuFailureReason Reason { get; init; }

  public IReadOnlyList<Guid> StationIdsOutsideTheFestival { get; init; } = [];
}
