using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class StationRepository : IStationRepository
{
  private readonly TimeProvider _timeProvider;
  private readonly GastronomyAppDbContext _dbContext;

  public StationRepository(GastronomyAppDbContext dbContext, TimeProvider timeProvider)
  {
    _dbContext = dbContext;
    _timeProvider = timeProvider;
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

  public async Task<IReadOnlyList<Station>> FindAtFestivalWithOpenItemsAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.Stations.AsNoTracking()
                           .Where(station => station.IsActive && _dbContext.FestivalStations.Any(link => link.FestivalId == festivalId && link.StationId == station.Id))
                           .Include(station => station.StationOrders.Where(stationOrder => stationOrder.FestivalId == festivalId))
                           .ThenInclude(stationOrder => stationOrder.Items.Where(item => item.FulfilledAtUtc == null))
                           .ThenInclude(item => item.CatalogItem)
                           .OrderBy(station => station.SortOrder)
                           .ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<Station>> FindAllAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    List<Station> stations = await _dbContext.Stations.AsNoTracking()
                                             .OrderBy(station => station.SortOrder)
                                             .Include(station => station.Device)
                                             .Include(station => station.EnrolmentInvitation)
                                             .Include(station => station.FestivalStations.Where(link => festivalId != null && link.FestivalId == festivalId.Value))
                                             .ToListAsync(cancellationToken);

    var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

    foreach (var station in stations.Where(station => station.EnrolmentInvitation?.IsOutstandingAt(nowUtc) == false))
      station.EnrolmentInvitation = null;

    return stations;
  }

  public async Task<Station?> FindByIdAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return await _dbContext.Stations.FirstOrDefaultAsync(station => station.Id == stationId, cancellationToken);
  }

  public async Task<bool> ExistsAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return await _dbContext.Stations.AsNoTracking().AnyAsync(station => station.Id == stationId, cancellationToken);
  }

  public async Task AddAsync(Station station, CancellationToken cancellationToken)
  {
    await _dbContext.Stations.AddAsync(station, cancellationToken);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
