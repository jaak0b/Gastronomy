using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class CatalogRepository : ICatalogRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public CatalogRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<Festival?> FindWithMenuAsync(Guid festivalId, IReadOnlyCollection<Guid> orderableItemIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderableItemIds);

    List<Guid> orderableItems = orderableItemIds.ToList();

    List<Guid> stationIdsAtTheFestival = await _dbContext.FestivalStations.AsNoTracking().Where(link => link.FestivalId == festivalId && link.Station.IsActive).Select(link => link.StationId).ToListAsync(cancellationToken);

    return await _dbContext.Festivals.AsNoTracking()
                           .Where(festival => festival.Id == festivalId)
                           .Include(festival => festival.Stations.Where(link => link.Station.IsActive))
                           .ThenInclude(link => link.Station)
                           .Include(festival => festival.CatalogItems.Where(menuRow => orderableItems.Contains(menuRow.CatalogItemId) && menuRow.CatalogItem.Category.IsActive))
                           .ThenInclude(menuRow => menuRow.CatalogItem)
                           .ThenInclude(item => item.Category)
                           .Include(festival => festival.CatalogItems.Where(menuRow => orderableItems.Contains(menuRow.CatalogItemId) && menuRow.CatalogItem.Category.IsActive))
                           .ThenInclude(menuRow => menuRow.CatalogItem)
                           .ThenInclude(item => item.StationAssignments.Where(assignment => assignment.FestivalId == festivalId && stationIdsAtTheFestival.Contains(assignment.StationId)))
                           .FirstOrDefaultAsync(cancellationToken);
  }
}
