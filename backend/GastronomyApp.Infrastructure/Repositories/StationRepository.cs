using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class StationRepository : IStationRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public StationRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<IReadOnlyCollection<Station>> FindAtFestivalAsync(Guid festivalId,
                                                                      CancellationToken cancellationToken)
  {
    return await (from station in _dbContext.Stations
                  join link in _dbContext.FestivalStations
                    on station.Id equals link.StationId
                  where link.FestivalId == festivalId && station.IsActive
                  orderby station.SortOrder
                  select station)
                 .ToListAsync(cancellationToken);
  }
}
