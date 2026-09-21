namespace GastronomyApp.Core.Results;

public enum StationQueueFailureReason
{
  StationUnknown,
  NoRunningFestival,
  StationNotAtTheFestival,
  UnknownOrderItemId,
  ItemNotFulfilled,
  OrderNotAtThisStation,
  NotAnAsItComesOrder
}
