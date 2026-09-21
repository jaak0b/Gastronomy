using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class StationOrderVisibilityService
{
  public ErrorOr<StationOrder> HideFromAsItComesQueue(StationOrder stationOrder)
  {
    ArgumentNullException.ThrowIfNull(stationOrder);

    if (stationOrder.DeliveryMode != DeliveryMode.AsItComes)
      return Refusal.StationQueue.NotAnAsItComesOrder(stationOrder.Id);

    stationOrder.IsHiddenFromAsItComesQueue = true;

    return stationOrder;
  }
}
