using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Services;

public sealed class CatalogService
{
  private readonly ICatalogRepository _catalogRepository;
  private readonly ItemOrderability _orderability;
  private readonly RunningFestivalLookup _runningFestival;

  public CatalogService(ICatalogRepository catalogRepository, ItemOrderability orderability, RunningFestivalLookup runningFestival)
  {
    _catalogRepository = catalogRepository;
    _orderability = orderability;
    _runningFestival = runningFestival;
  }

  public async Task<CatalogAtFestival?> ReadRunningFestivalCatalogAsync(CancellationToken cancellationToken)
  {
    var runningFestival = await _runningFestival.FindAsync(cancellationToken);

    if (runningFestival is null)
      return null;

    var catalog = await _catalogRepository.ReadAtFestivalAsync(runningFestival.Id, runningFestival.Name, cancellationToken);

    HashSet<Guid> orderableItemIds = (await _orderability.FindOrderableItemIdsAsync(runningFestival.Id, cancellationToken)).ToHashSet();

    List<CatalogItemRow> items = catalog.Items.Where(item => orderableItemIds.Contains(item.ItemId)).ToList();
    HashSet<Guid> categoryIdsWithItems = items.Select(item => item.CategoryId).ToHashSet();

    List<CatalogCategoryRow> categories = catalog.Categories.Where(category => categoryIdsWithItems.Contains(category.CategoryId)).ToList();

    return catalog with
           {
             Items = items,
             Categories = categories
           };
  }
}
