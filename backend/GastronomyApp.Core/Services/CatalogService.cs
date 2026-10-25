using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Contracts.Catalog;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Results;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class CatalogService
{
  private readonly IGastronomyDatabase _database;
  private readonly TypeAdapterConfig _mapping;
  private readonly ItemOrderability _orderability;
  private readonly RunningFestivalLookup _runningFestival;

  public CatalogService(IGastronomyDatabase database, TypeAdapterConfig mapping, ItemOrderability orderability, RunningFestivalLookup runningFestival)
  {
    _database = database;
    _mapping = mapping;
    _orderability = orderability;
    _runningFestival = runningFestival;
  }

  public async Task<CatalogView> ReadRunningFestivalAsync(CancellationToken cancellationToken)
  {
    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return CatalogView.Empty;

    List<Guid> orderableItemIds = (await _orderability.FindOrderableItemIdsAsync(festival.Id, cancellationToken)).ToList();

    var stations = await _database.FestivalStations.AsNoTracking().ActiveAt(festival.Id).OrderBy(link => link.Station.SortOrder).ProjectToType<CatalogStationView>(_mapping).ToListAsync(cancellationToken);

    var menu = await _database.FestivalCatalogItems.AsNoTracking()
                              .Where(menuRow => menuRow.FestivalId == festival.Id && orderableItemIds.Contains(menuRow.CatalogItemId) && menuRow.CatalogItem.Category.IsActive)
                              .OrderBy(menuRow => menuRow.CatalogItem.SortOrder)
                              .ProjectToType<CatalogMenuEntry>(_mapping)
                              .ToListAsync(cancellationToken);

    return new(festival.Adapt<RunningFestivalView>(_mapping), menu.Select(entry => entry.Category).DistinctBy(category => category.CategoryId).OrderBy(category => category.SortOrder).ToList(), menu.Select(entry => entry.Item).ToList(), stations);
  }
}
