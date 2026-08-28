namespace GastronomyApp.Api.Contracts;

public sealed record CatalogCategoryView(string Name, int SortOrder);

public sealed record CatalogItemView(
    Guid Id,
    string Name,
    string CategoryName,
    int PriceCents,
    int SortOrder,
    bool IsAvailable,
    IReadOnlyList<Guid> StationIds);

public sealed record CatalogStationView(Guid Id, string Name, int SortOrder);

public sealed record CatalogView(
    string Version,
    IReadOnlyList<CatalogCategoryView> Categories,
    IReadOnlyList<CatalogItemView> Items,
    IReadOnlyList<CatalogStationView> Stations);
