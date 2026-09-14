using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public enum StationOrderVisibilityFailureReason
{
  NotAnAsItComesOrder
}

public sealed record StationOrderVisibilityFailure
{
  public required StationOrderVisibilityFailureReason Reason { get; init; }
}

public sealed class StationOrderVisibilityService
{
  public Result<StationOrder, StationOrderVisibilityFailure> HideFromAsItComesQueue(StationOrder slice)
  {
    ArgumentNullException.ThrowIfNull(slice);

    if (slice.DeliveryMode != DeliveryMode.AsItComes)
    {
      return Result<StationOrder, StationOrderVisibilityFailure>.Failed(new()
                                                                        {
                                                                          Reason =
                                                                            StationOrderVisibilityFailureReason
                                                                              .NotAnAsItComesOrder
                                                                        });
    }

    slice.IsHiddenFromAsItComesQueue = true;

    return Result<StationOrder, StationOrderVisibilityFailure>.Success(slice);
  }
}
