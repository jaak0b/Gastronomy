namespace GastronomyApp.Api.ErrorHandling;

public sealed record ProblemDescription
{
  public required int StatusCode { get; init; }

  public required ApiError Error { get; init; }
}
