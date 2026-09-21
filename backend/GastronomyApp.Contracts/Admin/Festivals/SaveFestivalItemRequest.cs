namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record SaveFestivalItemRequest
{
  public required int PriceCents { get; init; }

  public required IReadOnlyList<Guid>? StationIds { get; init; }
}
