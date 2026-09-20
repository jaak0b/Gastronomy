namespace GastronomyApp.Api.Contracts;

public sealed record StationEstimateListView(IReadOnlyList<StationEstimateView> Stations);
