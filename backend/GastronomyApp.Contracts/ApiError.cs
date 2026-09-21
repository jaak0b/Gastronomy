namespace GastronomyApp.Contracts;

public sealed record ApiError
{
  public required string Code { get; init; }

  public required string MessageKey { get; init; }

  public required IReadOnlyDictionary<string, string> Parameters { get; init; }

  public string? Details { get; init; }
}
