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
                         CancellationToken cancellationToken) =>
                  {
                    return Results.Ok(await catalogReader.ReadAsync(dbContext, cancellationToken));
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
  public async Task<CatalogView> ReadAsync(GastronomyAppDbContext dbContext, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(dbContext);

    List<Station> stations = await dbContext.Stations
                                            .Where(station => station.IsActive)
                                            .OrderBy(station => station.SortOrder)
                                            .ToListAsync(cancellationToken);

    HashSet<Guid> activeStationIds = [.. stations.Select(station => station.Id)];

    List<CatalogCategory> categories = await dbContext.CatalogCategories
                                                      .Where(category => category.IsActive)
                                                      .OrderBy(category => category.SortOrder)
                                                      .ToListAsync(cancellationToken);

    List<Guid> activeCategoryIds = [.. categories.Select(category => category.Id)];

    List<CatalogItem> items = await dbContext.CatalogItems
                                             .Where(item => item.IsActive && activeCategoryIds.Contains(item.CategoryId))
                                             .OrderBy(item => item.SortOrder)
                                             .ToListAsync(cancellationToken);

    HashSet<Guid> itemIds = [.. items.Select(item => item.Id)];

    List<ItemStationAssignment> assignments = await dbContext.ItemStationAssignments
                                                             .Where(assignment => itemIds.Contains(assignment.CatalogItemId))
                                                             .ToListAsync(cancellationToken);

    List<CatalogItemView> itemViews =
    [
      .. items.Select(item => new CatalogItemView(item.Id,
                                                  item.CategoryId,
                                                  item.Name,
                                                  item.PriceCents,
                                                  item.SortOrder,
                                                  item.IsAvailable,
                                                  item.ProductionMinutes,
                                                  [
                                                    .. assignments
                                                      .Where(assignment =>
                                                               assignment.CatalogItemId == item.Id
                                                               && activeStationIds.Contains(assignment.StationId))
                                                      .Select(assignment => assignment.StationId)
                                                  ]))
    ];

    HashSet<Guid> categoryIdsWithItems = [.. items.Select(item => item.CategoryId)];

    List<CatalogCategoryView> categoryViews =
    [
      .. categories.Where(category => categoryIdsWithItems.Contains(category.Id))
                   .Select(category => new CatalogCategoryView(category.Id,
                                                               category.Name,
                                                               category.ColourHex,
                                                               category.SortOrder))
    ];

    return new(categoryViews,
               itemViews,
               [.. stations.Select(station => new CatalogStationView(station.Id, station.Name, station.SortOrder))]);
  }
}
