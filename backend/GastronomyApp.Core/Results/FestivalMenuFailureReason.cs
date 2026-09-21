namespace GastronomyApp.Core.Results;

public enum FestivalMenuFailureReason
{
  FestivalNotFound = 1,
  CatalogItemNotFound = 2,
  MenuRowNotFound = 3,
  StationsDoNotBelongToTheFestival = 5,
  NoStationPreparesTheItem = 6,
  FestivalIsRunning = 7
}
