namespace GastronomyApp.Api.Contracts;

public sealed record AdminCategoryListView(IReadOnlyList<AdminCategoryView> Categories);
