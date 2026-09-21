namespace GastronomyApp.Contracts.Admin.Catalog;

public sealed record AdminCategoryListView(IReadOnlyList<AdminCategoryView> Categories);
