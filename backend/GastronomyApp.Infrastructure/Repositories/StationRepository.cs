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

  public async Task<IReadOnlyCollection<Station>> FindActiveAsync(CancellationToken cancellationToken)
  {
    return await _dbContext.Stations
                           .Where(station => station.IsActive)
                           .OrderBy(station => station.SortOrder)
                           .ToListAsync(cancellationToken);
  }
}
