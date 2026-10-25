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
}
