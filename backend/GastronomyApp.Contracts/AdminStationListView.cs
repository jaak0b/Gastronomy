namespace GastronomyApp.Contracts;

public sealed record AdminStationListView(IReadOnlyList<AdminStationView> Stations);
