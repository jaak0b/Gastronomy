namespace GastronomyApp.Contracts.Stations;

public sealed record StationItemSelectionRequest
{
  public required IReadOnlyList<Guid>? OrderItemIds { get; init; }
}
