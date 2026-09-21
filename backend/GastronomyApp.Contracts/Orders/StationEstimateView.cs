namespace GastronomyApp.Contracts.Orders;

public sealed record StationEstimateView(Guid StationId, double QueuedMinutes);
