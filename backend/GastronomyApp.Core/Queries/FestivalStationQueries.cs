using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Queries;

public static class FestivalStationQueries
{
  public static IQueryable<FestivalStation> ActiveAt(this IQueryable<FestivalStation> links, Guid festivalId)
  {
    ArgumentNullException.ThrowIfNull(links);

    return links.WhereFestivalIs(festivalId).Where(link => link.Station.IsActive);
  }
}
