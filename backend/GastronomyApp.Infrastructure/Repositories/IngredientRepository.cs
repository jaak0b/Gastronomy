using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class IngredientRepository : IIngredientRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public IngredientRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<IReadOnlyList<Ingredient>> FindAllOrderedByNameAsync(CancellationToken cancellationToken)
  {
    return await _dbContext.Ingredients.AsNoTracking().OrderBy(ingredient => ingredient.Name).ToListAsync(cancellationToken);
  }

  public async Task<Ingredient?> FindByIdAsync(Guid ingredientId, CancellationToken cancellationToken)
  {
    return await _dbContext.Ingredients.FirstOrDefaultAsync(ingredient => ingredient.Id == ingredientId, cancellationToken);
  }

  public async Task AddAsync(Ingredient ingredient, CancellationToken cancellationToken)
  {
    await _dbContext.Ingredients.AddAsync(ingredient, cancellationToken);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
