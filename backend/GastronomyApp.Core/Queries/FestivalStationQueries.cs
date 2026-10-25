using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Queries;

public static class FestivalStationQueries
{
  public static IQueryable<FestivalStation> ActiveAt(this IQueryable<FestivalStation> links, Guid festivalId)
  {
    ArgumentNullException.ThrowIfNull(links);

    return links.Where(link => link.FestivalId == festivalId && link.Station.IsActive);
  }
}
