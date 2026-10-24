using GastronomyApp.Core.Announcements;

namespace GastronomyApp.Core.Entities;

[Raises(HubEvent.ConfigurationChanged)]
public sealed class FestivalStationShape : AreaShape
{
  public required Guid FestivalStationId { get; set; }

  public FestivalStation FestivalStation { get; set; } = null!;
}
