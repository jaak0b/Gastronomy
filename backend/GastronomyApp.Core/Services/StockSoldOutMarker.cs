using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Core.Services;

public sealed class StockSoldOutMarker
{
  private readonly IngredientAmountComparison _amountComparison;
  private readonly ILogger<StockSoldOutMarker> _logger;
  private readonly IIngredientStockRepository _repository;

  public StockSoldOutMarker(IIngredientStockRepository repository, IngredientAmountComparison amountComparison, ILogger<StockSoldOutMarker> logger)
  {
    _repository = repository;
    _amountComparison = amountComparison;
    _logger = logger;
  }

  public async Task<IReadOnlyList<FestivalCatalogItem>> MarkItemsWithoutEnoughStockSoldOutAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    IReadOnlyList<FestivalIngredient> limitedStock = await _repository.FindActiveWithAvailableAmountAsync(festivalId, cancellationToken);

    if (limitedStock.Count == 0)
      return [];

    IReadOnlyDictionary<Guid, double> consumedByIngredientId = await _repository.SumConsumedAmountsAsync(festivalId, cancellationToken);
    Dictionary<Guid, double> availableByIngredientId = limitedStock.ToDictionary(stock => stock.IngredientId, stock => stock.AvailableAmount!.Value);

    IReadOnlyList<FestivalCatalogItem> availableMenuRows = await _repository.FindAvailableMenuRowsWithRecipesAsync(festivalId, cancellationToken);

    List<FestivalCatalogItem> soldOut = availableMenuRows
                                       .Where(menuRow => menuRow.CatalogItem.Ingredients.Any(recipeLine => availableByIngredientId.TryGetValue(recipeLine.IngredientId, out var available)
                                                                                                           && !_amountComparison.IsAtLeast(available, consumedByIngredientId.GetValueOrDefault(recipeLine.IngredientId) + recipeLine.Amount)))
                                       .ToList();

    if (soldOut.Count == 0)
      return [];

    foreach (var menuRow in soldOut)
      menuRow.IsAvailable = false;

    await _repository.SaveChangesAsync(cancellationToken);

    _logger.LogInformation("{SoldOutCount} articles at the festival {FestivalId} were marked sold out because an ingredient has less left than one portion needs. Catalog item ids: {CatalogItemIds}.", soldOut.Count, festivalId, soldOut.Select(menuRow => menuRow.CatalogItemId).ToList());

    return soldOut;
  }
}
