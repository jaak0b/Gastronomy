namespace GastronomyApp.Api.Contracts;

public sealed record AdminCategoryView(Guid CategoryId, string Name, string ColourHex, int SortOrder, bool IsActive);
