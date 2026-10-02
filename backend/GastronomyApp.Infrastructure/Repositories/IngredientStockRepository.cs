using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
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

  public async Task<IReadOnlyList<FestivalIngredient>> FindUsedOnTheMenuAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.FestivalIngredients.AsNoTracking()
                           .Where(stock => stock.FestivalId == festivalId && _dbContext.CatalogItemIngredients.Any(recipeLine => recipeLine.IngredientId == stock.IngredientId && _dbContext.FestivalCatalogItems.Any(menuRow => menuRow.FestivalId == festivalId && menuRow.CatalogItemId == recipeLine.CatalogItemId)))
                           .Include(stock => stock.Ingredient)
                           .OrderBy(stock => stock.Ingredient.Name)
                           .ToListAsync(cancellationToken);
  }

  public async Task<FestivalIngredient?> FindAsync(Guid festivalId, Guid ingredientId, CancellationToken cancellationToken)
  {
    return await _dbContext.FestivalIngredients.FirstOrDefaultAsync(stock => stock.FestivalId == festivalId && stock.IngredientId == ingredientId, cancellationToken);
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
                           .Where(orderItem => orderItem.StationOrder.Order.FestivalId == festivalId)
                           .Join(_dbContext.CatalogItemIngredients,
                                 orderItem => orderItem.CatalogItemId,
                                 recipeLine => recipeLine.CatalogItemId,
                                 (orderItem, recipeLine) => recipeLine)
                           .GroupBy(recipeLine => recipeLine.IngredientId)
                           .Select(group => new
                                            {
                                              IngredientId = group.Key,
                                              ConsumedAmount = group.Sum(recipeLine => recipeLine.Amount)
                                            })
                           .ToDictionaryAsync(consumption => consumption.IngredientId, consumption => consumption.ConsumedAmount, cancellationToken);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
