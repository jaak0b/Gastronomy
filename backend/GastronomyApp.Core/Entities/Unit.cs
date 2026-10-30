using System.Collections.ObjectModel;
using GastronomyApp.Core.Announcements;

namespace GastronomyApp.Core.Entities;

[Raises(HubEvent.ConfigurationChanged)]
public sealed class Unit : INamed
{
  public required Guid Id { get; set; }

  public required string Name { get; set; }

  public Collection<Ingredient> Ingredients { get; } = [];
}
