namespace GastronomyApp.Api.Contracts;

public sealed record SaveFestivalItemRequest
{
  public required int PriceCents { get; init; }

  public required IReadOnlyList<Guid>? StationIds { get; init; }
}
