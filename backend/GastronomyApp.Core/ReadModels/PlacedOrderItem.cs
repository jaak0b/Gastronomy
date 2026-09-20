namespace GastronomyApp.Core.ReadModels;

public sealed record PlacedOrderItem
{
  public required Guid OrderItemId { get; init; }

  public required int UnitPriceCents { get; init; }

  public required bool IsFulfilled { get; init; }
}
