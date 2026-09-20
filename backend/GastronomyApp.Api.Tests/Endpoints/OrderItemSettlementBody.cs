namespace GastronomyApp.Api.Tests.Endpoints;

public sealed record OrderItemSettlementBody(int? PaidPriceCents, string? PaymentNotice = null);
