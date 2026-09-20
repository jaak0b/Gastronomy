namespace GastronomyApp.Core.Results;

public enum StationQueueFailureReason
{
  StationUnknown,
  NoRunningFestival,
  StationNotAtTheFestival,
  NoItemsSelected,
  UnknownOrderItemId,
  ItemNotFulfilled,
  OrderNotAtThisStation,
  NotAnAsItComesOrder
}
