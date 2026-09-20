using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.ReadModels;

public sealed record PlacedStationOrder
{
  public required Guid StationOrderId { get; init; }

  public required Guid StationId { get; init; }

  public required string StationName { get; init; }

  public required int StationOrderNumber { get; init; }

  public required DeliveryMode DeliveryMode { get; init; }

  public required IReadOnlyList<PlacedOrderItem> Items { get; init; }
}
