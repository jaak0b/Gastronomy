using System.Collections.ObjectModel;

namespace GastronomyApp.Core.Entities;

public sealed class Station
{
  public required Guid Id { get; set; }

  public required string Name { get; set; }

  public required int SortOrder { get; set; }

  public required bool IsActive { get; set; }

  public Guid? DeviceId { get; set; }

  public Guid? EnrolmentInvitationId { get; set; }

  public Device? Device { get; set; }

  public EnrolmentInvitation? EnrolmentInvitation { get; set; }

  public Collection<StationOrder> StationOrders { get; } = [];

  public Collection<FestivalStation> FestivalStations { get; } = [];

  public double QueuedMinutes()
  {
    var queuedMinutes = StationOrders.SelectMany(stationOrder => stationOrder.Items).Where(item => item.FulfilledAtUtc == null && !item.CatalogItem.IsQueueIndependent).Sum(item => item.CatalogItem.ProductionMinutes ?? 0);

    return Math.Round(queuedMinutes, 1);
  }
}
