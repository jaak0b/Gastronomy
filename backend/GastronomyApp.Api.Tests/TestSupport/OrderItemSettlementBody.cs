namespace GastronomyApp.Api.Tests.TestSupport;

public sealed record OrderItemSettlementBody(int? PaidPriceCents, string? PaymentNotice = null);
