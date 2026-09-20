namespace GastronomyApp.Core.Services;

public sealed record PutOnTheMenuRequest
{
  public required int PriceCents { get; init; }

  public required IReadOnlyList<Guid>? StationIds { get; init; }
}
