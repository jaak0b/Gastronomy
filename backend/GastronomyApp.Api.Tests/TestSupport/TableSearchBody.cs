namespace GastronomyApp.Api.Tests.TestSupport;

public sealed record TableSearchBody(string? OrderStatus, string? PaymentStatus, bool? IncludeCancelled);
