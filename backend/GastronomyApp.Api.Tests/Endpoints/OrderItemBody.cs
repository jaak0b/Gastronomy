namespace GastronomyApp.Api.Tests.Endpoints;

public sealed record OrderItemBody(Guid CatalogItemId, int UnitPriceCents, string? Note, Guid? StationId, OrderItemSettlementBody? Settlement = null);
