namespace GastronomyApp.Infrastructure;

public sealed record TransactionOutcome<TValue>
{
  public required TValue Value { get; init; }

  public required bool ShouldCommit { get; init; }
}
