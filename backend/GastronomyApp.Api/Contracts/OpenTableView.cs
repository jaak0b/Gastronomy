namespace GastronomyApp.Api.Contracts;

public sealed record OpenTableView(string TableName, int OpenAmountCents, IReadOnlyList<OpenOrderItemView> Items);
