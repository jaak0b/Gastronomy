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

  public async Task<IReadOnlyList<FestivalIngredient>> FindActiveUsedOnTheMenuAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _dbContext.FestivalIngredients.AsNoTracking()
                           .Where(stock => stock.FestivalId == festivalId
                                           && stock.Ingredient.IsActive
                                           && _dbContext.CatalogItemIngredients.Any(recipeLine => recipeLine.IngredientId == stock.IngredientId && _dbContext.FestivalCatalogItems.Any(menuRow => menuRow.FestivalId == festivalId && menuRow.CatalogItemId == recipeLine.CatalogItemId)))
                           .Include(stock => stock.Ingredient)
                           .ThenInclude(ingredient => ingredient.Unit)
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
                           .Join(_dbContext.CatalogItemIngredients, orderItem => orderItem.CatalogItemId, recipeLine => recipeLine.CatalogItemId, (orderItem, recipeLine) => recipeLine)
                           .GroupBy(recipeLine => recipeLine.IngredientId)
                           .Select(group => new
                                            {
                                              IngredientId = group.Key,
                                              ConsumedAmount = group.Sum(recipeLine => recipeLine.Amount)
                                            })
                           .ToDictionaryAsync(consumption => consumption.IngredientId, consumption => consumption.ConsumedAmount, cancellationToken);
  }

  public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<DateTime, double>>> SumConsumedAmountsByOrderTimeAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var consumption = await _dbContext.OrderItems.AsNoTracking()
                                      .Where(orderItem => orderItem.StationOrder.Order.FestivalId == festivalId)
                                      .Join(_dbContext.CatalogItemIngredients,
                                            orderItem => orderItem.CatalogItemId,
                                            recipeLine => recipeLine.CatalogItemId,
                                            (orderItem, recipeLine) => new
                                                                       {
                                                                         recipeLine.IngredientId,
                                                                         OrderedAtUtc = orderItem.StationOrder.Order.CreatedAtUtc,
                                                                         recipeLine.Amount
                                                                       })
                                      .GroupBy(consumed => new
                                                           {
                                                             consumed.IngredientId,
                                                             consumed.OrderedAtUtc
                                                           })
                                      .Select(group => new
                                                       {
                                                         group.Key.IngredientId,
                                                         group.Key.OrderedAtUtc,
                                                         ConsumedAmount = group.Sum(consumed => consumed.Amount)
                                                       })
                                      .ToListAsync(cancellationToken);

    return consumption.GroupBy(consumed => consumed.IngredientId).ToDictionary(ingredient => ingredient.Key, ingredient => (IReadOnlyDictionary<DateTime, double>)ingredient.ToDictionary(consumed => DateTime.SpecifyKind(consumed.OrderedAtUtc, DateTimeKind.Utc), consumed => consumed.ConsumedAmount));
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
