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
    CatalogAtFestival? catalog = await _catalogService.ReadRunningFestivalCatalogAsync(cancellationToken);

    return Results.Ok(catalog is null ? new CatalogView(null, [], [], []) : BuildCatalogView(catalog));
  }

  private CatalogView BuildCatalogView(CatalogAtFestival catalog)
  {
    return new(new RunningFestivalView(catalog.FestivalId, catalog.FestivalName),
               [
                 .. catalog.Categories.Select(category => new CatalogCategoryView(category.CategoryId,
                                                                                  category.Name,
                                                                                  category.ColourHex,
                                                                                  category.SortOrder))
               ],
               [
                 .. catalog.Items.Select(item => new CatalogItemView(item.ItemId,
                                                                     item.CategoryId,
                                                                     item.Name,
                                                                     item.PriceCents,
                                                                     item.SortOrder,
                                                                     item.IsAvailable,
                                                                     item.ProductionMinutes,
                                                                     item.IsQueueIndependent,
                                                                     item.StationIds))
               ],
               [
                 .. catalog.Stations.Select(station => new CatalogStationView(station.StationId,
                                                                              station.Name,
                                                                              station.SortOrder))
               ]);
  }
}
