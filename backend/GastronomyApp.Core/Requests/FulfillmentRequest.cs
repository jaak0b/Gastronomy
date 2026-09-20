namespace GastronomyApp.Core.Requests;

public sealed record FulfillmentRequest
{
  public required IReadOnlyList<Guid> OrderItemIds { get; init; }
}
