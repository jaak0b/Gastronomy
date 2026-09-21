namespace GastronomyApp.Contracts;

public sealed record AdminCategoryListView(IReadOnlyList<AdminCategoryView> Categories);
