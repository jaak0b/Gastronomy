namespace GastronomyApp.Core.Results;

public enum OrderValidationFailureReason
{
  NoItems,
  TableNameMissing,
  UnknownCatalogItemId,
  StationRequired,
  StationNotAssignedToItem,
  ItemHasNoStation,
  PriceOutOfRange,
  NoRunningFestival,
  OrderNumberCouldNotBeAllocated,
  SettlementCannotBeProcessed,
  ItemNotAvailable,
  ChosenStationNoLongerPreparesTheItem
}
