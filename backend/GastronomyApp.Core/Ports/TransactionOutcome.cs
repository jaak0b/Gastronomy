namespace GastronomyApp.Core.Ports;

public sealed record TransactionOutcome<TValue>
{
  public required TValue Value { get; init; }

  public required bool ShouldCommit { get; init; }
}
