namespace GastronomyApp.Core.ReadModels;

public sealed record StationEstimate
{
  public required Guid StationId { get; init; }

  public required double QueuedMinutes { get; init; }
}
