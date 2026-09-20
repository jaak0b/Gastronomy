namespace GastronomyApp.Core.Requests;

public sealed record FestivalPeriodRequest
{
  public required string? Name { get; init; }

  public required DateTime StartsAtUtc { get; init; }

  public required DateTime EndsAtUtc { get; init; }
}
