namespace GastronomyApp.Contracts.OpenItems;

public sealed record OpenItemsView(IReadOnlyList<OpenTableView> Tables, int ItemsWithoutAnOrderCount);
