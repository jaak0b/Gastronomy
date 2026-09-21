namespace GastronomyApp.Core.Results;

public enum StationAdministrationFailureReason
{
  FestivalNotFound = 1,
  StationNotFound = 2,
  StationHasUnfulfilledItems = 4,
  ItemsWouldHaveNoStation = 5
}
