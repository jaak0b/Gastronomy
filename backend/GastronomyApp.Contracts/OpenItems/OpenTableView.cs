namespace GastronomyApp.Contracts.OpenItems;

public sealed record OpenTableView(string TableName, int OpenAmountCents, IReadOnlyList<OpenOrderItemView> Items);
