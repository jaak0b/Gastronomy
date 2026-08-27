namespace GastronomyApp.Api.Contracts;

public sealed record HealthView(string Status, string? EventSession, int PrintersOnline, int PrintersTotal);
