namespace GastronomyApp.Core.Results;

public enum CatalogItemAdministrationFailureReason
{
  FestivalNotFound,
  ItemNotFound,
  NameTaken,
  ProductionMinutesOutOfRange,
  CategoryUnknown,
  CategoryIsSwitchedOff,
  ItemIsOnTheRunningFestivalsMenu
}
