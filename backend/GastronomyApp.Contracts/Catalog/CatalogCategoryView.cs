namespace GastronomyApp.Contracts.Catalog;

public sealed record CatalogCategoryView(Guid CategoryId, string Name, string ColourHex, int SortOrder);
