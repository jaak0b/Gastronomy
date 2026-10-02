using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class CatalogItemIngredientRepository : ICatalogItemIngredientRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public CatalogItemIngredientRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<CatalogItemIngredient?> FindAsync(Guid catalogItemId, Guid ingredientId, CancellationToken cancellationToken)
  {
    return await _dbContext.CatalogItemIngredients.FirstOrDefaultAsync(recipeLine => recipeLine.CatalogItemId == catalogItemId && recipeLine.IngredientId == ingredientId, cancellationToken);
  }

  public async Task AddAsync(CatalogItemIngredient recipeLine, CancellationToken cancellationToken)
  {
    await _dbContext.CatalogItemIngredients.AddAsync(recipeLine, cancellationToken);
  }

  public void Remove(CatalogItemIngredient recipeLine)
  {
    _dbContext.CatalogItemIngredients.Remove(recipeLine);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
