namespace GastronomyApp.Contracts.Orders;

public sealed record PlaceOrderRequest
{
  public required Guid ClientOrderId { get; init; }

  public required string? TableName { get; init; }

  public required IReadOnlyList<OrderItemRequest>? Items { get; init; }

  public IReadOnlyList<OrderDeliveryModeRequest>? DeliveryModes { get; init; }
}
