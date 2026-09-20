using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class FestivalStationRepository : IFestivalStationRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public FestivalStationRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<FestivalStation?> FindLinkAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return await _dbContext.FestivalStations.FirstOrDefaultAsync(link => link.FestivalId == festivalId && link.StationId == stationId, cancellationToken);
  }

  public async Task<IReadOnlyList<ItemStationAssignment>> FindAssignmentsAtStationAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return await _dbContext.ItemStationAssignments.Where(assignment => assignment.FestivalId == festivalId && assignment.StationId == stationId).ToListAsync(cancellationToken);
  }

  public async Task<int> CountUnfulfilledItemsAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return await _dbContext.StationOrders.AsNoTracking().Where(stationOrder => stationOrder.FestivalId == festivalId && stationOrder.StationId == stationId).SelectMany(stationOrder => stationOrder.Items).CountAsync(item => item.FulfilledAtUtc == null, cancellationToken);
  }

  public async Task AddLinkAsync(FestivalStation link, CancellationToken cancellationToken)
  {
    await _dbContext.FestivalStations.AddAsync(link, cancellationToken);
  }

  public void RemoveLink(FestivalStation link)
  {
    _dbContext.FestivalStations.Remove(link);
  }

  public void RemoveAssignments(IReadOnlyCollection<ItemStationAssignment> assignments)
  {
    ArgumentNullException.ThrowIfNull(assignments);

    _dbContext.ItemStationAssignments.RemoveRange(assignments);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
