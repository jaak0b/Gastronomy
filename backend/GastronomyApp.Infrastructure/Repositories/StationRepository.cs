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

  public async Task<IReadOnlyCollection<Guid>> FindStationIdsWithItemsToHandOutAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.OrderItems.AsNoTracking().WhereNotHandedOut().WhereFestivalIs(festivalId).Select(item => item.StationOrder.StationId).Distinct().ToListAsync(cancellationToken);
  }

  public async Task<Station?> FindByIdAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return await _dbContext.Stations.FirstOrDefaultAsync(station => station.Id == stationId, cancellationToken);
  }
}
