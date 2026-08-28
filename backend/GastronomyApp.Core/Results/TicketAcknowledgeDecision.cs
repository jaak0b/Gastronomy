namespace GastronomyApp.Core.Results;

public sealed record StationPrintability
{
  public required bool IsFaulty { get; init; }

  public required bool IsOnline { get; init; }

  public required bool IsPaperEnd { get; init; }

  public required bool IsCoverOpen { get; init; }

  public required bool IsInErrorState { get; init; }

  public required bool IsEnabled { get; init; }
}
