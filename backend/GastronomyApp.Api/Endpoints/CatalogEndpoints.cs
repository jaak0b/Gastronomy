using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.RateLimiting;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class CatalogEndpoints
{
  public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapGet("/api/catalog",
                  async (GastronomyAppDbContext dbContext,
                         CatalogReader catalogReader,
                         RunningFestivalLookup runningFestivalLookup,
                         CancellationToken cancellationToken) =>
                  {
                    var festival = await runningFestivalLookup.FindAsync(cancellationToken);

                    return Results.Ok(await catalogReader.ReadAsync(dbContext, festival, cancellationToken));
                  })
          .RequireAuthorization()
          .RequireStaffDevice()
          .RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

    return routes;
  }
}

public sealed class CatalogChangeAnnouncer
{
  private readonly HubNotificationDispatcher _dispatcher;

  public CatalogChangeAnnouncer(HubNotificationDispatcher dispatcher)
  {
    _dispatcher = dispatcher;
  }

  public async Task AnnounceAsync(CancellationToken cancellationToken)
  {
    await _dispatcher.PushCatalogChangedAsync(cancellationToken);
  }
}

public sealed class CatalogReader
{
  public async Task<CatalogView> ReadAsync(GastronomyAppDbContext dbContext,
                                           Festival? runningFestival,
                                           CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    if (runningFestival is null)
    {
      return new(null, [], [], []);
    }

    var festivalId = runningFestival.Id;

    List<Station> stations = await (from station in dbContext.Stations
                                    join link in dbContext.FestivalStations
                                      on station.Id equals link.StationId
                                    where link.FestivalId == festivalId && station.IsActive
                                    orderby station.SortOrder
                                    select station)
                                   .ToListAsync(cancellationToken);

    HashSet<Guid> stationIdsAtTheFestival = [.. stations.Select(station => station.Id)];

    List<CatalogCategory> categories = await dbContext.CatalogCategories
                                                      .Where(category => category.IsActive)
                                                      .OrderBy(category => category.SortOrder)
                                                      .ToListAsync(cancellationToken);

    List<Guid> activeCategoryIds = [.. categories.Select(category => category.Id)];

    List<MenuRow> menuRows = await (from menuItem in dbContext.FestivalCatalogItems
                                    join item in dbContext.CatalogItems
                                      on menuItem.CatalogItemId equals item.Id
                                    where menuItem.FestivalId == festivalId
                                          && item.IsActive
                                          && activeCategoryIds.Contains(item.CategoryId)
                                    orderby item.SortOrder
                                    select new MenuRow(item, menuItem.PriceCents, menuItem.IsAvailable))
                                   .ToListAsync(cancellationToken);

    List<ItemStationAssignment> assignments = await dbContext.ItemStationAssignments
                                                             .Where(assignment => assignment.FestivalId == festivalId)
                                                             .ToListAsync(cancellationToken);

    List<CatalogItemView> itemViews =
    [
      .. menuRows.Select(row => new CatalogItemView(row.Item.Id,
                                                    row.Item.CategoryId,
                                                    row.Item.Name,
                                                    row.PriceCents,
                                                    row.Item.SortOrder,
                                                    row.IsAvailable,
                                                    row.Item.ProductionMinutes,
                                                    [
                                                      .. assignments
                                                        .Where(assignment =>
                                                                 assignment.CatalogItemId == row.Item.Id
                                                                 && stationIdsAtTheFestival.Contains(assignment.StationId))
                                                        .Select(assignment => assignment.StationId)
                                                    ]))
    ];

    HashSet<Guid> categoryIdsWithItems = [.. menuRows.Select(row => row.Item.CategoryId)];

    List<CatalogCategoryView> categoryViews =
    [
      .. categories.Where(category => categoryIdsWithItems.Contains(category.Id))
                   .Select(category => new CatalogCategoryView(category.Id,
                                                               category.Name,
                                                               category.ColourHex,
                                                               category.SortOrder))
    ];

    return new(new(runningFestival.Id, runningFestival.Name),
               categoryViews,
               itemViews,
               [.. stations.Select(station => new CatalogStationView(station.Id, station.Name, station.SortOrder))]);
  }

  private sealed record MenuRow(CatalogItem Item, int PriceCents, bool IsAvailable);
}
