using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
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

  public async Task<IReadOnlyList<CatalogItem>> FindAllOrderedAsync(CancellationToken cancellationToken)
  {
    return await _dbContext.CatalogItems.OrderBy(item => item.SortOrder).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyCollection<ItemStationAssignment>> FindAssignmentsAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken)
  {
    return await _dbContext.ItemStationAssignments.Where(assignment => assignment.FestivalId == festivalId && assignment.CatalogItemId == catalogItemId).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<ItemStationAssignment>> FindAssignmentsAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.ItemStationAssignments.AsNoTracking().Where(assignment => assignment.FestivalId == festivalId).ToListAsync(cancellationToken);
  }

  public async Task<FestivalCatalogItem?> FindMenuRowAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken)
  {
    return await _dbContext.FestivalCatalogItems.FirstOrDefaultAsync(menuRow => menuRow.FestivalId == festivalId && menuRow.CatalogItemId == catalogItemId, cancellationToken);
  }

  public async Task<IReadOnlyList<FestivalCatalogItem>> FindMenuRowsAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.FestivalCatalogItems.AsNoTracking().Where(menuRow => menuRow.FestivalId == festivalId).ToListAsync(cancellationToken);
  }

  public async Task<bool> IsNameTakenAsync(string name, Guid? itemBeingSaved, CancellationToken cancellationToken)
  {
    IQueryable<CatalogItem> candidates = _dbContext.CatalogItems.AsNoTracking().Where(item => item.Name == name);

    if (itemBeingSaved is { } savedItemId)
      candidates = candidates.Where(item => item.Id != savedItemId);

    return await candidates.AnyAsync(cancellationToken);
  }

  public async Task AddAsync(CatalogItem item, CancellationToken cancellationToken)
  {
    await _dbContext.CatalogItems.AddAsync(item, cancellationToken);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
