using GastronomyApp.Core.Announcements;

namespace GastronomyApp.Core.Entities;

[Raises(HubEvent.ConfigurationChanged)]
public sealed class Table : AreaShape
{
  public required Guid Id { get; set; }

  public required string Name { get; set; }

  public required bool IsHidden { get; set; }
}
