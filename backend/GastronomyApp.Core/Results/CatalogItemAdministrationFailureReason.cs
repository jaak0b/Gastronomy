namespace GastronomyApp.Core.Results;

public enum CatalogItemAdministrationFailureReason
{
  FestivalNotFound,
  ItemNotFound,
  NameMissing,
  NameTaken,
  ProductionMinutesOutOfRange,
  CategoryUnknown,
  CategoryIsSwitchedOff,
  ItemIsOnTheRunningFestivalsMenu
}
