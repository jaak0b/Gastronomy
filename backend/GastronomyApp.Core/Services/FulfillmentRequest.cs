namespace GastronomyApp.Core.Services;

public sealed record FulfillmentRequest
{
  public required IReadOnlyList<Guid> OrderItemIds { get; init; }
}
