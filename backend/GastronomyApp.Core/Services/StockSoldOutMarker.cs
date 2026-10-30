using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Core.Services;

public sealed class StockSoldOutMarker
{
  private readonly IngredientAmountComparison _amountComparison;
  private readonly IGastronomyDatabase _database;
  private readonly ILogger<StockSoldOutMarker> _logger;

  public StockSoldOutMarker(IGastronomyDatabase database, IngredientAmountComparison amountComparison, ILogger<StockSoldOutMarker> logger)
  {
    _database = database;
    _amountComparison = amountComparison;
    _logger = logger;
  }

  public async Task<IReadOnlyList<FestivalCatalogItem>> MarkItemsWithoutEnoughStockSoldOutAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    List<FestivalIngredient> limitedStock = await _database.FestivalIngredients.AsNoTracking().Where(stock => stock.FestivalId == festivalId && stock.AvailableAmount != null).ToListAsync(cancellationToken);

    if (limitedStock.Count == 0)
      return [];

    Dictionary<Guid, double> consumedByIngredientId = await _database.OrderItems.AsNoTracking()
                                                                     .WhereFestivalIs(festivalId)
                                                                     .ConsumedIngredients(_database.CatalogItemIngredients)
                                                                     .GroupBy(consumed => consumed.IngredientId)
                                                                     .Select(group => new
                                                                                      {
                                                                                        IngredientId = group.Key,
                                                                                        ConsumedAmount = group.Sum(consumed => consumed.Amount)
                                                                                      })
                                                                     .ToDictionaryAsync(consumption => consumption.IngredientId, consumption => consumption.ConsumedAmount, cancellationToken);
    Dictionary<Guid, double> availableByIngredientId = limitedStock.ToDictionary(stock => stock.IngredientId, stock => stock.AvailableAmount!.Value);

    List<FestivalCatalogItem> availableMenuRows = await _database.FestivalCatalogItems.Where(menuRow => menuRow.FestivalId == festivalId && menuRow.IsAvailable).Include(menuRow => menuRow.CatalogItem).ThenInclude(item => item.Ingredients).ToListAsync(cancellationToken);

    List<FestivalCatalogItem> soldOut = availableMenuRows
                                       .Where(menuRow => menuRow.CatalogItem.Ingredients.Any(recipeLine => availableByIngredientId.TryGetValue(recipeLine.IngredientId, out var available)
                                                                                                           && !_amountComparison.IsAtLeast(available, consumedByIngredientId.GetValueOrDefault(recipeLine.IngredientId) + recipeLine.Amount)))
                                       .ToList();

    if (soldOut.Count == 0)
      return [];

    foreach (var menuRow in soldOut)
      menuRow.IsAvailable = false;

    await _database.SaveChangesAsync(cancellationToken);

    _logger.LogInformation("{SoldOutCount} articles at the festival {FestivalId} were marked sold out because an ingredient has less left than one portion needs. Catalog item ids: {CatalogItemIds}.", soldOut.Count, festivalId, soldOut.Select(menuRow => menuRow.CatalogItemId).ToList());

    return soldOut;
  }
}
