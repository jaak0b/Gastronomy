namespace GastronomyApp.Core.Results;

public enum RoutingFailureReason
{
  ItemHasNoStation,
  StationRequired,
  StationNotAssignedToItem,
  ChosenStationNoLongerPreparesTheItem
}
