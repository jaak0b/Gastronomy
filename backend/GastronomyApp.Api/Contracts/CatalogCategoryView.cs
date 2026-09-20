namespace GastronomyApp.Api.Contracts;

public sealed record CatalogCategoryView(Guid CategoryId, string Name, string ColourHex, int SortOrder);
