using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class StationOrderVisibilityService
{
  public Result<StationOrder, StationOrderVisibilityFailure> HideFromAsItComesQueue(StationOrder stationOrder)
  {
    ArgumentNullException.ThrowIfNull(stationOrder);

    if (stationOrder.DeliveryMode != DeliveryMode.AsItComes)
    {
      return Result<StationOrder, StationOrderVisibilityFailure>.Failed(new()
                                                                        {
                                                                          Reason =
                                                                            StationOrderVisibilityFailureReason
                                                                              .NotAnAsItComesOrder
                                                                        });
    }

    stationOrder.IsHiddenFromAsItComesQueue = true;

    return Result<StationOrder, StationOrderVisibilityFailure>.Success(stationOrder);
  }
}
