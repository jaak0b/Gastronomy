namespace GastronomyApp.Api.Contracts;

public sealed record StationFulfilledView(IReadOnlyList<StationOrderQueueView> StationOrders);
