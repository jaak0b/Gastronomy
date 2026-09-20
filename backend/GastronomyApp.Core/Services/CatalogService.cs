using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Services;

public sealed class CatalogService
{
  private readonly ICatalogRepository _catalogRepository;
  private readonly IClock _clock;
  private readonly IFestivalRepository _festivalRepository;
  private readonly ItemOrderability _orderability;

  public CatalogService(ICatalogRepository catalogRepository,
                        IFestivalRepository festivalRepository,
                        ItemOrderability orderability,
                        IClock clock)
  {
    _catalogRepository = catalogRepository;
    _festivalRepository = festivalRepository;
    _orderability = orderability;
    _clock = clock;
  }

  public async Task<CatalogAtFestival?> ReadRunningFestivalCatalogAsync(CancellationToken cancellationToken)
  {
    Festival? runningFestival = await _festivalRepository.FindRunningAsync(_clock.UtcNow, cancellationToken);

    if (runningFestival is null)
    {
      return null;
    }

    CatalogAtFestival catalog = await _catalogRepository.ReadAtFestivalAsync(runningFestival.Id,
                                                                            runningFestival.Name,
                                                                            cancellationToken);

    HashSet<Guid> orderableItemIds =
      [.. await _orderability.FindOrderableItemIdsAsync(runningFestival.Id, cancellationToken)];

    List<CatalogItemRow> items = [.. catalog.Items.Where(item => orderableItemIds.Contains(item.ItemId))];
    HashSet<Guid> categoryIdsWithItems = [.. items.Select(item => item.CategoryId)];

    List<CatalogCategoryRow> categories =
    [
      .. catalog.Categories.Where(category => categoryIdsWithItems.Contains(category.CategoryId))
    ];

    return catalog with { Items = items, Categories = categories };
  }
}
