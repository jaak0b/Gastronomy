namespace GastronomyApp.Core.Results;

public enum FestivalStationFailureReason
{
  FestivalNotFound = 1,
  StationNotFound = 2,
  StationLinkNotFound = 3,
  StationHasUnfulfilledItems = 4,
  ItemsWouldHaveNoStation = 5
}
