namespace GastronomyApp.Api.Tests.TestSupport;

public sealed record OrderItemBody(Guid CatalogItemId, int UnitPriceCents, string? Note, Guid? StationId, OrderItemSettlementBody? Settlement = null);
