using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed class StationService
{
  public bool HasOutstandingInvitation(Station station)
  {
    ArgumentNullException.ThrowIfNull(station);

    return station.EnrolmentInvitation is not null;
  }

  public bool IsAtAnyFestival(Station station)
  {
    ArgumentNullException.ThrowIfNull(station);

    return station.FestivalStations.Count != 0;
  }

  public double QueuedMinutesOf(Station station)
  {
    ArgumentNullException.ThrowIfNull(station);

    return Math.Round(station.StationOrders.SelectMany(stationOrder => stationOrder.Items).Where(item => item.FulfilledAtUtc == null && !item.CatalogItem.IsQueueIndependent).Sum(item => item.CatalogItem.ProductionMinutes ?? 0), 1);
  }
}
