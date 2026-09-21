using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

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

  public async Task<ErrorOr<Festival>> ReadRunningFestivalCatalogAsync(CancellationToken cancellationToken)
  {
    var runningFestival = await _runningFestival.FindAsync(cancellationToken);

    if (runningFestival is null)
      return Refusal.Catalog.NoRunningFestival();

    IReadOnlyList<Guid> orderableItemIds = await _orderability.FindOrderableItemIdsAsync(runningFestival.Id, cancellationToken);

    var festivalWithMenu = await _catalogRepository.FindWithMenuAsync(runningFestival.Id, orderableItemIds, cancellationToken);

    if (festivalWithMenu is null)
      return Refusal.Catalog.NoRunningFestival();

    return festivalWithMenu;
  }
}
