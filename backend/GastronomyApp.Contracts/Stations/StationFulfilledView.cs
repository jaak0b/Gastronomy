namespace GastronomyApp.Contracts.Stations;

public sealed record StationFulfilledView(IReadOnlyList<StationOrderQueueView> StationOrders);
