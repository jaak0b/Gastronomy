namespace GastronomyApp.Api.Contracts;

public sealed record OpenTableView(
  string TableName,
  int OpenAmountCents,
  int GivenAwayAmountCents,
  IReadOnlyList<OpenOrderItemView> Items,
  IReadOnlyList<GivenAwayOrderItemView> GivenAwayItems);
