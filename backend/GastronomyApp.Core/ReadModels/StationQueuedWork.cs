using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.ReadModels;

public sealed record StationQueuedWork
{
  public required Guid StationId { get; init; }

  public required QueuedWork Work { get; init; }
}
