namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record SetAvailabilityRequest
{
  public required bool IsAvailable { get; init; }
}
