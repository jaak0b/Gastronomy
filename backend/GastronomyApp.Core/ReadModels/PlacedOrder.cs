namespace GastronomyApp.Core.ReadModels;

public sealed record PlacedOrder
{
  public required Guid OrderId { get; init; }

  public required int GlobalOrderNumber { get; init; }

  public required DateTime CreatedAtUtc { get; init; }

  public required IReadOnlyList<PlacedStationOrder> StationOrders { get; init; }
}
