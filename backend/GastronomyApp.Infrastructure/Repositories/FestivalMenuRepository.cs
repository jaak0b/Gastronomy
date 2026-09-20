using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class FestivalMenuRepository : IFestivalMenuRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public FestivalMenuRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<FestivalCatalogItem?> FindMenuRowAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken)
  {
    return await _dbContext.FestivalCatalogItems.FirstOrDefaultAsync(menuRow => menuRow.FestivalId == festivalId && menuRow.CatalogItemId == catalogItemId, cancellationToken);
  }

  public async Task<bool> CatalogItemExistsAsync(Guid catalogItemId, CancellationToken cancellationToken)
  {
    return await _dbContext.CatalogItems.AsNoTracking().AnyAsync(item => item.Id == catalogItemId, cancellationToken);
  }

  public async Task<IReadOnlyList<Guid>> FindStationIdsAtFestivalAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.FestivalStations.AsNoTracking().Where(link => link.FestivalId == festivalId).Select(link => link.StationId).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<ItemStationAssignment>> FindAssignmentsAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken)
  {
    return await _dbContext.ItemStationAssignments.Where(assignment => assignment.FestivalId == festivalId && assignment.CatalogItemId == catalogItemId).ToListAsync(cancellationToken);
  }

  public async Task AddMenuRowAsync(FestivalCatalogItem menuRow, CancellationToken cancellationToken)
  {
    await _dbContext.FestivalCatalogItems.AddAsync(menuRow, cancellationToken);
  }

  public void RemoveMenuRow(FestivalCatalogItem menuRow)
  {
    _dbContext.FestivalCatalogItems.Remove(menuRow);
  }

  public async Task AddAssignmentAsync(ItemStationAssignment assignment, CancellationToken cancellationToken)
  {
    await _dbContext.ItemStationAssignments.AddAsync(assignment, cancellationToken);
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
