namespace GastronomyApp.Api.Contracts;

public sealed record AdminStationListView(IReadOnlyList<AdminStationView> Stations);
