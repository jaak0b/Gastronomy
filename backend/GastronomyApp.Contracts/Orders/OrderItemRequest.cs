using GastronomyApp.Contracts.OpenItems;

namespace GastronomyApp.Contracts.Orders;

public sealed record OrderItemRequest
{
  public required Guid CatalogItemId { get; init; }

  public required int UnitPriceCents { get; init; }

  public string? Note { get; init; }

  public Guid? StationId { get; init; }

  public OrderSettlementLineRequest? Settlement { get; init; }
}
