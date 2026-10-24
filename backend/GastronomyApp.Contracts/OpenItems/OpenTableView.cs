namespace GastronomyApp.Contracts.OpenItems;

public sealed record OpenTableView(Guid TableId, string TableName, int OpenAmountCents, IReadOnlyList<OpenOrderItemView> Items);
