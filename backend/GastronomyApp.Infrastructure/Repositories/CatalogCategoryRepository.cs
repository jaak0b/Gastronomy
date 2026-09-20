using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class CatalogCategoryRepository : ICatalogCategoryRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public CatalogCategoryRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<CatalogCategory?> FindByIdAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    return await _dbContext.CatalogCategories.FirstOrDefaultAsync(category => category.Id == categoryId, cancellationToken);
  }

  public async Task<IReadOnlyList<CatalogCategory>> FindAllOrderedAsync(CancellationToken cancellationToken)
  {
    return await _dbContext.CatalogCategories.OrderBy(category => category.SortOrder).ToListAsync(cancellationToken);
  }

  public async Task<bool> HoldsActiveItemsAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    return await _dbContext.CatalogItems.AsNoTracking().AnyAsync(item => item.CategoryId == categoryId && item.IsActive, cancellationToken);
  }

  public async Task AddAsync(CatalogCategory category, CancellationToken cancellationToken)
  {
    await _dbContext.CatalogCategories.AddAsync(category, cancellationToken);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
