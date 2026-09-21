namespace GastronomyApp.Contracts;

public sealed record ProblemDescription
{
  public required int StatusCode { get; init; }

  public required ApiError Error { get; init; }
}
