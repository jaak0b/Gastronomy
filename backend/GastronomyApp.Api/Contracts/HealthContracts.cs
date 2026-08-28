namespace GastronomyApp.Api.Contracts;

public sealed record HealthView(string Status, int PrintersOnline, int PrintersTotal);
