namespace GastronomyApp.Core.Results;

public sealed record TransactionOutcome<TValue>
{
  public required TValue Value { get; init; }

  public required bool ShouldCommit { get; init; }
}
