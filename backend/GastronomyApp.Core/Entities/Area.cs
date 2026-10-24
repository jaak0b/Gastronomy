using System.Collections.ObjectModel;
using GastronomyApp.Core.Announcements;

namespace GastronomyApp.Core.Entities;

[Raises(HubEvent.ConfigurationChanged)]
public sealed class Area
{
  public required Guid Id { get; set; }

  public required Guid FestivalId { get; set; }

  public required string Name { get; set; }

  public required int SortOrder { get; set; }

  public required bool IsHidden { get; set; }

  public Festival Festival { get; set; } = null!;

  public Collection<Table> Tables { get; } = [];

  public Collection<FestivalStationShape> StationShapes { get; } = [];
}
