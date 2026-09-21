namespace GastronomyApp.Contracts.Admin.Stations;

public sealed record AdminStationListView(IReadOnlyList<AdminStationView> Stations);
