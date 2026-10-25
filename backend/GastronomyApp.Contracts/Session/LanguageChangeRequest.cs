namespace GastronomyApp.Contracts.Session;

public sealed record LanguageChangeRequest
{
  public required string? Language { get; init; }
}
