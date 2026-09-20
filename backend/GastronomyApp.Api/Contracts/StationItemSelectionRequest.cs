namespace GastronomyApp.Api.Contracts;

public sealed record StationItemSelectionRequest
{
  public required IReadOnlyList<Guid>? OrderItemIds { get; init; }
}
