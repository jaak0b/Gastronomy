namespace GastronomyApp.Contracts;

public sealed record StationFulfilledView(IReadOnlyList<StationOrderQueueView> StationOrders);
