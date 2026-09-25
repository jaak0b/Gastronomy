using GastronomyApp.Core.Announcements;

namespace GastronomyApp.Core.Entities;

[Raises(HubEvent.ConfigurationChanged, HubEvent.OrdersChanged)]
public sealed class FestivalStation
{
  public required Guid Id { get; set; }

  public required Guid FestivalId { get; set; }

  public required Guid StationId { get; set; }

  public required int NextStationOrderNumber { get; set; }

  public Festival Festival { get; set; } = null!;

  public Station Station { get; set; } = null!;
}
