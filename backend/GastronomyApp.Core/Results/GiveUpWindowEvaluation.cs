namespace GastronomyApp.Core.Results;

public sealed record SuspensionPeriod
{
  public required DateTime StartedAtUtc { get; init; }

  public DateTime? EndedAtUtc { get; init; }
}

public sealed record GiveUpWindowEvaluation
{
  public required bool HasReachedGiveUpWindow { get; init; }

  public required bool HasReachedOuterBound { get; init; }

  public required TimeSpan AccumulatedUnsuspendedTime { get; init; }
}
