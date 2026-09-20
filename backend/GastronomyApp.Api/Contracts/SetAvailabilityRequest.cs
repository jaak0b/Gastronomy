namespace GastronomyApp.Api.Contracts;

public sealed record SetAvailabilityRequest
{
  public required bool IsAvailable { get; init; }
}
