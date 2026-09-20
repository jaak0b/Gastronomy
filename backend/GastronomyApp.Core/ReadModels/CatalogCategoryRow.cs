namespace GastronomyApp.Core.ReadModels;

public sealed record CatalogCategoryRow(Guid CategoryId, string Name, string ColourHex, int SortOrder);
