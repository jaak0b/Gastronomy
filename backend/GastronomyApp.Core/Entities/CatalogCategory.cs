using System.Collections.ObjectModel;
using GastronomyApp.Core.Announcements;

namespace GastronomyApp.Core.Entities;

[Raises(HubEvent.ConfigurationChanged)]
public sealed class CatalogCategory
{
  public required Guid Id { get; set; }

  public required string Name { get; set; }

  public required string ColourHex { get; set; }

  public required int SortOrder { get; set; }

  public required bool IsActive { get; set; }

  public Collection<CatalogItem> Items { get; } = [];
}
