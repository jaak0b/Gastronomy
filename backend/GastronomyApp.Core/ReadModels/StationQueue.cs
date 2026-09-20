namespace GastronomyApp.Core.ReadModels;

public sealed record StationQueue
{
  public required Guid StationId { get; init; }

  public required string StationName { get; init; }

  public required IReadOnlyList<QueuedStationOrder> Orders { get; init; }

  public required IReadOnlyList<QueuedStationOrder> AsItComesOrders { get; init; }
}
