namespace GastronomyApp.Api.Contracts;

public sealed record OrderItemsSettledEvent(
  IReadOnlyList<Guid> OrderItemIds,
  IReadOnlyList<string> TableNames);
