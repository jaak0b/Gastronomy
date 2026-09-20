using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Results;

public sealed record StationQueueChange
{
  public required StationQueue Queue { get; init; }

  public required IReadOnlyList<OrderStatusChange> OrderStatusChanges { get; init; }
}
