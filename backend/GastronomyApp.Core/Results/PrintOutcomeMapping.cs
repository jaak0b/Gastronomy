using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Results;

public sealed record PrintOutcomeMapping
{
  public required PrintJobStatus JobStatus { get; init; }
  public required bool ShouldRetryAutomatically { get; init; }
  public PrintFailureReason? FailureReason { get; init; }
}
