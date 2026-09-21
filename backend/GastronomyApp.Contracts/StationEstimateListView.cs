namespace GastronomyApp.Contracts;

public sealed record StationEstimateListView(IReadOnlyList<StationEstimateView> Stations);
