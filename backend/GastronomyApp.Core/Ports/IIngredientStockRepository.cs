using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IIngredientStockRepository
{
  public Task<IReadOnlyList<FestivalIngredient>> FindActiveWithAvailableAmountAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<FestivalCatalogItem>> FindAvailableMenuRowsWithRecipesAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task<IReadOnlyDictionary<Guid, double>> SumConsumedAmountsAsync(Guid festivalId, CancellationToken cancellationToken);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
