namespace GastronomyApp.Api.Tests.Endpoints;

public sealed record SettleLineBody(Guid OrderItemId, int? PaidPriceCents, string? PaymentNotice = null);
