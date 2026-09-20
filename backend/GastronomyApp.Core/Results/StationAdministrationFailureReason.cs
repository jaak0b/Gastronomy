namespace GastronomyApp.Core.Results;

public enum StationAdministrationFailureReason
{
  FestivalNotFound = 1,
  StationNotFound = 2,
  NameMissing = 3,
  StationHasUnfulfilledItems = 4,
  ItemsWouldHaveNoStation = 5
}
