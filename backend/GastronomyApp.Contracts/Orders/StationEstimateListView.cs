namespace GastronomyApp.Contracts.Orders;

public sealed record StationEstimateListView(IReadOnlyList<StationEstimateView> Stations);
