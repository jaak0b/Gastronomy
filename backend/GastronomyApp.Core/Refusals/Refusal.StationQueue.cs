using ErrorOr;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class StationQueue
  {
    public static Error StationUnknown()
    {
      return Unauthorized("StationUnknown", "The tablet named a station the database no longer holds, so its session is refused.");
    }

    public static Error NoRunningFestival()
    {
      return Conflict("station.noFestivalIsRunning", "No festival is running, so this station has no queue to show.", new() { [MetadataKeys.ProblemCode] = "NoRunningFestival" });
    }

    public static Error StationNotAtTheFestival()
    {
      return Conflict("station.notPartOfTheFestival", "This station does not take part in the running festival, so it has no queue there.", new() { [MetadataKeys.ProblemCode] = "StationNotAtTheFestival" });
    }

    public static Error UnknownOrderItemId(Guid orderItemId)
    {
      return UnprocessableEntity("station.itemNotAtThisStation", $"The order item {orderItemId} is not one of the items this station holds.", new() { [MetadataKeys.ProblemCode] = ProblemCodes.UnprocessableEntity });
    }

    public static Error ItemNotFulfilled(Guid orderItemId)
    {
      return Conflict("station.changeNotSaved", $"The order item {orderItemId} was to be put back although it was never handed out.", new() { [MetadataKeys.ProblemCode] = "ItemNotFulfilled" });
    }

    public static Error OrderNotAtThisStation(Guid stationOrderId)
    {
      return UnprocessableEntity("station.orderNotAtThisStation", $"The station order {stationOrderId} does not belong to this station at the running festival.", new() { [MetadataKeys.ProblemCode] = ProblemCodes.UnprocessableEntity });
    }

    public static Error NotAnAsItComesOrder(Guid stationOrderId)
    {
      return Conflict("station.changeNotSaved", $"The station order {stationOrderId} is handed over together, so it never stands in the column an employee can hide it from.", new() { [MetadataKeys.ProblemCode] = "CannotHideTogetherOrder" });
    }
  }
}
