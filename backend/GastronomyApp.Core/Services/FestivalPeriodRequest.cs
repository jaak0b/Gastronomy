namespace GastronomyApp.Core.Services;

public sealed record FestivalPeriodRequest
{
  public required string? Name { get; init; }

  public required DateTime StartsAtUtc { get; init; }

  public required DateTime EndsAtUtc { get; init; }
}
