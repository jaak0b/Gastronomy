namespace GastronomyApp.Contracts;

public sealed record AdminItemListView(IReadOnlyList<AdminItemView> Items);
