namespace GastronomyApp.Api.Tests.TestSupport;

public sealed record SettleLineBody(Guid OrderItemId, int? PaidPriceCents, string? PaymentNotice = null);
