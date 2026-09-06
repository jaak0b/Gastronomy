namespace GastronomyApp.Api.Contracts;

public sealed record HealthView(string Status, int ActiveStationCount, int StationsWithADeviceCount);
