using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
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
    return await _dbContext.Stations
                           .Join(_dbContext.FestivalStations,
                                 station => station.Id,
                                 link => link.StationId,
                                 (station, link) => new { Station = station, Link = link })
                           .Where(joined => joined.Link.FestivalId == festivalId
                                            && joined.Station.IsActive)
                           .OrderBy(joined => joined.Station.SortOrder)
                           .Select(joined => joined.Station)
                           .ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<AdministeredStation>> FindAdministeredAsync(Guid? festivalId,
                                                                              DateTime nowUtc,
                                                                              CancellationToken cancellationToken)
  {
    return await _dbContext.Stations
                           .AsNoTracking()
                           .OrderBy(station => station.SortOrder)
                           .Select(station =>
                                     new AdministeredStation(station.Id,
                                                             station.Name,
                                                             station.SortOrder,
                                                             station.IsActive,
                                                             station.DeviceId != null,
                                                             _dbContext.Devices
                                                                       .Where(device => device.Id == station.DeviceId)
                                                                       .Select(device => (DateTime?)device.LastSeenAtUtc)
                                                                       .FirstOrDefault(),
                                                             _dbContext.EnrolmentInvitations
                                                                       .Any(invitation => invitation.Id == station.EnrolmentInvitationId
                                                                                          && invitation.ConsumedAtUtc == null
                                                                                          && invitation.ExpiresAtUtc > nowUtc),
                                                             festivalId != null
                                                             && _dbContext.FestivalStations
                                                                          .Any(link => link.FestivalId == festivalId.Value
                                                                                       && link.StationId == station.Id)))
                           .ToListAsync(cancellationToken);
  }

  public async Task<Station?> FindByIdAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return await _dbContext.Stations
                           .FirstOrDefaultAsync(station => station.Id == stationId, cancellationToken);
  }

  public async Task<bool> ExistsAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return await _dbContext.Stations
                           .AsNoTracking()
                           .AnyAsync(station => station.Id == stationId, cancellationToken);
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
