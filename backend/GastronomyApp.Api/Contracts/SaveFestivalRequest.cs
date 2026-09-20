namespace GastronomyApp.Api.Contracts;

public sealed record SaveFestivalRequest
{
  public required string? Name { get; init; }

  public required DateTime StartsAtUtc { get; init; }

  public required DateTime EndsAtUtc { get; init; }
}
