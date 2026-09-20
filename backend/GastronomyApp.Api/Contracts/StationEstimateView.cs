namespace GastronomyApp.Api.Contracts;

public sealed record StationEstimateView(Guid StationId, double QueuedMinutes);
