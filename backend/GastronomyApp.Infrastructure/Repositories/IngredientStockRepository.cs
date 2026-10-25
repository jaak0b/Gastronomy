using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Queries;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class IngredientStockRepository : IIngredientStockRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public IngredientStockRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<IReadOnlyList<FestivalIngredient>> FindActiveWithAvailableAmountAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.FestivalIngredients.AsNoTracking().Where(stock => stock.FestivalId == festivalId && stock.AvailableAmount != null && stock.Ingredient.IsActive).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<FestivalCatalogItem>> FindAvailableMenuRowsWithRecipesAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.FestivalCatalogItems.Where(menuRow => menuRow.FestivalId == festivalId && menuRow.IsAvailable).Include(menuRow => menuRow.CatalogItem).ThenInclude(item => item.Ingredients).ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyDictionary<Guid, double>> SumConsumedAmountsAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.OrderItems.AsNoTracking()
                           .WhereFestivalIs(festivalId)
                           .ConsumedIngredients(_dbContext.CatalogItemIngredients)
                           .GroupBy(consumed => consumed.IngredientId)
                           .Select(group => new
                                            {
                                              IngredientId = group.Key,
                                              ConsumedAmount = group.Sum(consumed => consumed.Amount)
                                            })
                           .ToDictionaryAsync(consumption => consumption.IngredientId, consumption => consumption.ConsumedAmount, cancellationToken);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
