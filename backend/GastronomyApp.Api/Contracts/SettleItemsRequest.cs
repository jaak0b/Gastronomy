namespace GastronomyApp.Api.Contracts;

public sealed record SettleItemsRequest
{
  public required IReadOnlyList<SettleLineRequest>? Lines { get; init; }
}
