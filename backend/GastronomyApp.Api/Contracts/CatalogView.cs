namespace GastronomyApp.Api.Contracts;

public sealed record CatalogView(RunningFestivalView? Festival, IReadOnlyList<CatalogCategoryView> Categories, IReadOnlyList<CatalogItemView> Items, IReadOnlyList<CatalogStationView> Stations);
