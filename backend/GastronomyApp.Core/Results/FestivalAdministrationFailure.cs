namespace GastronomyApp.Core.Results;

public sealed record FestivalAdministrationFailure
{
  public required FestivalAdministrationFailureReason Reason { get; init; }

  public string? OverlappingFestivalName { get; init; }

  public Guid? OffendingFestivalId { get; init; }
}
