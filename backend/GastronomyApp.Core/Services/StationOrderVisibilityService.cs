using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class StationOrderVisibilityService
{
  public Result<StationOrder, Failure<StationOrderVisibilityFailureReason>> HideFromAsItComesQueue(StationOrder stationOrder)
  {
    ArgumentNullException.ThrowIfNull(stationOrder);

    if (stationOrder.DeliveryMode != DeliveryMode.AsItComes)
      return Result<StationOrder, Failure<StationOrderVisibilityFailureReason>>.Failed(new() { Reason = StationOrderVisibilityFailureReason.NotAnAsItComesOrder });

    stationOrder.IsHiddenFromAsItComesQueue = true;

    return Result<StationOrder, Failure<StationOrderVisibilityFailureReason>>.Success(stationOrder);
  }
}
