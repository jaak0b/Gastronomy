namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record AdminFestivalListView(IReadOnlyList<AdminFestivalView> Festivals);
