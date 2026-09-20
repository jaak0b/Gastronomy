namespace GastronomyApp.Core.Results;

public sealed record Failure<TReason>
{
  public required TReason Reason { get; init; }
}
