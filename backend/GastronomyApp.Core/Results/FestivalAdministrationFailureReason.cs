namespace GastronomyApp.Core.Results;

public enum FestivalAdministrationFailureReason
{
  FestivalNotFound = 1,
  NameMissing = 2,
  PeriodInvalid = 3,
  PeriodOverlapsAnotherFestival = 4,
  FestivalIsRunning = 5
}
