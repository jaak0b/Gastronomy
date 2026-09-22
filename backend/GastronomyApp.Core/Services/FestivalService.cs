using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed class FestivalService
{
  public int StationCountOf(Festival festival)
  {
    ArgumentNullException.ThrowIfNull(festival);

    return festival.Stations.Count;
  }

  public int MenuItemCountOf(Festival festival)
  {
    ArgumentNullException.ThrowIfNull(festival);

    return festival.CatalogItems.Count;
  }
}
