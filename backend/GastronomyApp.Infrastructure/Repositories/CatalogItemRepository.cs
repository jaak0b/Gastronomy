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
    return await _dbContext.CatalogItems
        .FirstOrDefaultAsync(item => item.Id == catalogItemId, cancellationToken);
  }

  public async Task<IReadOnlyCollection<ItemStationAssignment>> FindAssignmentsAsync(
      Guid catalogItemId,
      CancellationToken cancellationToken)
  {
    return await _dbContext.ItemStationAssignments
        .Where(assignment => assignment.CatalogItemId == catalogItemId)
        .ToListAsync(cancellationToken);
  }
}
