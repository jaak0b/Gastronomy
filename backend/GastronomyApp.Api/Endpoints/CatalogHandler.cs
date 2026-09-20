using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Endpoints;

public sealed class CatalogHandler
{
  private readonly CatalogService _catalogService;

  public CatalogHandler(CatalogService catalogService)
  {
    _catalogService = catalogService;
  }

  public async Task<IResult> ReadAsync(CancellationToken cancellationToken)
  {
    var catalog = await _catalogService.ReadRunningFestivalCatalogAsync(cancellationToken);

    if (catalog is null)
      return Results.Ok(new CatalogView(null, [], [], []));

    return Results.Ok(BuildCatalogView(catalog));
  }

  private CatalogView BuildCatalogView(CatalogAtFestival catalog)
  {
    return new(new(catalog.FestivalId, catalog.FestivalName),
               catalog.Categories.Select(category => new CatalogCategoryView(category.CategoryId, category.Name, category.ColourHex, category.SortOrder)).ToList(),
               catalog.Items.Select(item => new CatalogItemView(item.ItemId, item.CategoryId, item.Name, item.PriceCents, item.SortOrder, item.IsAvailable, item.ProductionMinutes, item.IsQueueIndependent, item.StationIds)).ToList(),
               catalog.Stations.Select(station => new CatalogStationView(station.StationId, station.Name, station.SortOrder)).ToList());
  }
}
