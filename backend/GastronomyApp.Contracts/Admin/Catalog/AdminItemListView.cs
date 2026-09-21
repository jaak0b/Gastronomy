namespace GastronomyApp.Contracts.Admin.Catalog;

public sealed record AdminItemListView(IReadOnlyList<AdminItemView> Items);
