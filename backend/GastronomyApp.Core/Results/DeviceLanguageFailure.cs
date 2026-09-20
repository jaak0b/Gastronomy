namespace GastronomyApp.Core.Results;

public sealed record DeviceLanguageFailure
{
  public required DeviceLanguageFailureReason Reason { get; init; }
}
