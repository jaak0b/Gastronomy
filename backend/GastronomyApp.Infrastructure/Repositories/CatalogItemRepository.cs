using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class CatalogItemRepository : ICatalogItemRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public CatalogItemRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<CatalogItem?> FindByIdAsync(Guid catalogItemId, CancellationToken cancellationToken)
  {
    return await _dbContext.CatalogItems.FirstOrDefaultAsync(item => item.Id == catalogItemId, cancellationToken);
  }

  public async Task<IReadOnlyList<CatalogItem>> FindByIdsAsync(IReadOnlyCollection<Guid> catalogItemIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(catalogItemIds);

    List<Guid> requestedIds = catalogItemIds.ToList();

    return await _dbContext.CatalogItems.AsNoTracking().Where(item => requestedIds.Contains(item.Id)).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyCollection<ItemStationAssignment>> FindAssignmentsAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken)
  {
    return await _dbContext.ItemStationAssignments.Where(assignment => assignment.FestivalId == festivalId && assignment.CatalogItemId == catalogItemId).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<ItemStationAssignment>> FindTimedAssignmentsIncludingOpenItemsAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.ItemStationAssignments.AsNoTracking()
                           .Where(assignment => assignment.FestivalId == festivalId && assignment.CatalogItem.ProductionMinutes != null && assignment.Station.IsActive && _dbContext.FestivalStations.Any(link => link.FestivalId == festivalId && link.StationId == assignment.StationId))
                           .Include(assignment => assignment.CatalogItem)
                           .Include(assignment => assignment.Station)
                           .ThenInclude(station => station.StationOrders.Where(stationOrder => stationOrder.FestivalId == festivalId))
                           .ThenInclude(stationOrder => stationOrder.Items.Where(item => item.FulfilledAtUtc == null))
                           .ThenInclude(item => item.CatalogItem)
                           .OrderBy(assignment => assignment.CatalogItem.SortOrder)
                           .ThenBy(assignment => assignment.Station.SortOrder)
                           .ToListAsync(cancellationToken);
  }

  public async Task<FestivalCatalogItem?> FindMenuRowAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken)
  {
    return await _dbContext.FestivalCatalogItems.FirstOrDefaultAsync(menuRow => menuRow.FestivalId == festivalId && menuRow.CatalogItemId == catalogItemId, cancellationToken);
  }
}
