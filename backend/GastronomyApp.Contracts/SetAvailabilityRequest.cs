namespace GastronomyApp.Contracts;

public sealed record SetAvailabilityRequest
{
  public required bool IsAvailable { get; init; }
}
