using System.Collections.ObjectModel;
using GastronomyApp.Core.Announcements;

namespace GastronomyApp.Core.Entities;

[Raises(HubEvent.ConfigurationChanged)]
public sealed class CatalogItem
{
  public required Guid Id { get; set; }

  public required string Name { get; set; }

  public required Guid CategoryId { get; set; }

  public required int SortOrder { get; set; }

  public required bool IsActive { get; set; }

  public double? ProductionMinutes { get; set; }

  public bool IsQueueIndependent { get; set; }

  public CatalogCategory Category { get; set; } = null!;

  public Collection<FestivalCatalogItem> FestivalCatalogItems { get; } = [];

  public Collection<ItemStationAssignment> StationAssignments { get; } = [];
}
