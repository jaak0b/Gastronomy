namespace GastronomyApp.Core.Requests;

public sealed record OrderAcceptanceItemRequest
{
  public required Guid CatalogItemId { get; init; }

  public required int UnitPriceCents { get; init; }

  public string? Note { get; init; }

  public Guid? StationId { get; init; }

  public OrderSettlementLineTerms? Settlement { get; init; }
}
