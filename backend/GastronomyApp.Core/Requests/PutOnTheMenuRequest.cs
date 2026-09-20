namespace GastronomyApp.Core.Requests;

public sealed record PutOnTheMenuRequest
{
  public required int PriceCents { get; init; }

  public required IReadOnlyList<Guid>? StationIds { get; init; }
}
