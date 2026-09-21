namespace GastronomyApp.Core.Results;

public enum OrderValidationFailureReason
{
  UnknownCatalogItemId,
  StationRequired,
  StationNotAssignedToItem,
  ItemHasNoStation,
  NoRunningFestival,
  OrderNumberCouldNotBeAllocated,
  SettlementCannotBeProcessed,
  ItemNotAvailable,
  ChosenStationNoLongerPreparesTheItem
}
