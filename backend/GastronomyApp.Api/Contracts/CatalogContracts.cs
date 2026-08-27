namespace GastronomyApp.Api.Contracts;

public sealed record CatalogCategoryView(string Name, int SortOrder);

public sealed record CatalogItemView(
    Guid Id,
    string Name,
    string CategoryName,
    int PriceCents,
    int SortOrder,
    bool IsAvailable,
    IReadOnlyList<Guid> LocationIds);

public sealed record CatalogLocationView(Guid Id, string Name, int SortOrder);

public sealed record TableSuggestionView(string Label, int SortOrder);

public sealed record CatalogView(
    string Version,
    IReadOnlyList<CatalogCategoryView> Categories,
    IReadOnlyList<CatalogItemView> Items,
    IReadOnlyList<CatalogLocationView> Locations,
    IReadOnlyList<TableSuggestionView> TableSuggestions);
