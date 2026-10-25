using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Queries;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class StationRepository : IStationRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public StationRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<IReadOnlyCollection<Station>> FindAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.Stations.Join(_dbContext.FestivalStations,
                                          station => station.Id,
                                          link => link.StationId,
                                          (station, link) => new
                                                             {
                                                               Station = station,
                                                               Link = link
                                                             })
                           .Where(joined => joined.Link.FestivalId == festivalId && joined.Station.IsActive)
                           .OrderBy(joined => joined.Station.SortOrder)
                           .Select(joined => joined.Station)
                           .ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyCollection<Guid>> FindStationIdsWithItemsToHandOutAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.OrderItems.AsNoTracking().WhereNotHandedOut().WhereFestivalIs(festivalId).Select(item => item.StationOrder.StationId).Distinct().ToListAsync(cancellationToken);
  }

  public async Task<Station?> FindByIdAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return await _dbContext.Stations.FirstOrDefaultAsync(station => station.Id == stationId, cancellationToken);
  }
}
