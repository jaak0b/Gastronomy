namespace GastronomyApp.Contracts.OpenItems;

public sealed record SettleItemsRequest
{
  public required IReadOnlyList<SettleLineRequest>? Lines { get; init; }
}
