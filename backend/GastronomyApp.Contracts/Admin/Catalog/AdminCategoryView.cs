namespace GastronomyApp.Contracts.Admin.Catalog;

public sealed record AdminCategoryView(Guid CategoryId, string Name, string ColourHex, int SortOrder, bool IsActive);
