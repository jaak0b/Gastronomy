namespace GastronomyApp.Api.Contracts;

public sealed record OpenItemsView(
  IReadOnlyList<OpenTableView> Tables,
  int ItemsWithoutAnOrderCount);
