namespace GastronomyApp.Api.Contracts;

public sealed record AdminItemListView(IReadOnlyList<AdminItemView> Items);
