namespace GastronomyApp.Contracts;

public sealed record OpenItemsView(IReadOnlyList<OpenTableView> Tables, int ItemsWithoutAnOrderCount);
