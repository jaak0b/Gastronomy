using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class StationOrderService
{
  public bool IsInAsItComesColumn(StationOrder stationOrder)
  {
    ArgumentNullException.ThrowIfNull(stationOrder);

    return stationOrder.DeliveryMode == DeliveryMode.AsItComes && !stationOrder.IsHiddenFromAsItComesQueue;
  }

  public ErrorOr<StationOrder> HideFromAsItComesQueue(StationOrder stationOrder)
  {
    ArgumentNullException.ThrowIfNull(stationOrder);

    if (stationOrder.DeliveryMode != DeliveryMode.AsItComes)
      return Refusal.StationQueue.NotAnAsItComesOrder(stationOrder.Id);

    stationOrder.IsHiddenFromAsItComesQueue = true;

    return stationOrder;
  }
}
