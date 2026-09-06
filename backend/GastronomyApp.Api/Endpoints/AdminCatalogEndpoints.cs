using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class AdminItemEndpoints
{
  public static IEndpointRouteBuilder MapAdminItemEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/items");

    group.MapGet(string.Empty,
                 async (AdminItemHandler handler,
                        CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    group.MapPost(string.Empty,
                  async (SaveItemRequest request,
                         AdminItemHandler handler,
                         CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken));

    group.MapPut("/{itemId:guid}",
                 async (Guid itemId,
                        SaveItemRequest request,
                        AdminItemHandler handler,
                        CancellationToken cancellationToken) => await handler.UpdateAsync(itemId, request, cancellationToken));

    group.MapPost("/{itemId:guid}/availability",
                  async (Guid itemId,
                         SetAvailabilityRequest request,
                         AdminItemHandler handler,
                         CancellationToken cancellationToken) =>
                    await handler.SetAvailabilityAsync(itemId, request, cancellationToken));

    group.MapPost("/{itemId:guid}/activate",
                  async (Guid itemId,
                         AdminItemHandler handler,
                         CancellationToken cancellationToken) => await handler.ActivateAsync(itemId, cancellationToken));

    group.MapPost("/{itemId:guid}/deactivate",
                  async (Guid itemId,
                         AdminItemHandler handler,
                         CancellationToken cancellationToken) => await handler.DeactivateAsync(itemId, cancellationToken));

    return routes;
  }
}

public sealed class AdminItemHandler
{
  private const int ShortestProductionMinutes = 0;
  private const int LongestProductionMinutes = 600;

  private readonly CatalogReader _catalogReader;
  private readonly GastronomyAppDbContext _dbContext;
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly ResultEnvelope _resultEnvelope;

  public AdminItemHandler(GastronomyAppDbContext dbContext,
                          CatalogReader catalogReader,
                          HubNotificationDispatcher dispatcher,
                          ResultEnvelope resultEnvelope)
  {
    _dbContext = dbContext;
    _catalogReader = catalogReader;
    _dispatcher = dispatcher;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    List<CatalogItem> items = await _dbContext.CatalogItems
                                             .AsNoTracking()
                                             .OrderBy(item => item.SortOrder)
                                             .ToListAsync(cancellationToken);

    List<ItemStationAssignment> assignments = await _dbContext.ItemStationAssignments
                                                             .AsNoTracking()
                                                             .ToListAsync(cancellationToken);

    List<AdminItemView> views =
    [
      .. items.Select(item => new AdminItemView(item.Id,
                                                item.Name,
                                                item.CategoryName,
                                                item.PriceCents,
                                                item.SortOrder,
                                                item.IsActive,
                                                item.IsAvailable,
                                                item.ProductionMinutes,
                                                [
                                                  .. assignments
                                                    .Where(assignment => assignment.CatalogItemId == item.Id)
                                                    .Select(assignment => assignment.StationId)
                                                ]))
    ];

    return Results.Ok(new AdminItemListView(views));
  }

  public async Task<IResult> CreateAsync(SaveItemRequest request, CancellationToken cancellationToken)
  {
    var refusal = Validate(request);

    if (refusal is null && !await AnyStationIsActiveAsync(request.StationIds!, cancellationToken))
    {
      refusal = ItemHasNoActiveStation();
    }

    if (refusal is not null)
    {
      return refusal;
    }

    var itemId = Guid.NewGuid();

    _dbContext.CatalogItems.Add(new()
                               {
                                 Id = itemId,
                                 Name = request.Name!,
                                 CategoryName = request.CategoryName!,
                                 PriceCents = request.PriceCents,
                                 SortOrder = request.SortOrder,
                                 IsActive = true,
                                 IsAvailable = true,
                                 ProductionMinutes = request.ProductionMinutes
                               });

    foreach (var stationId in request.StationIds!)
    {
      _dbContext.ItemStationAssignments.Add(new()
                                           {
                                             Id = Guid.NewGuid(),
                                             CatalogItemId = itemId,
                                             StationId = stationId
                                           });
    }

    await _dbContext.SaveChangesAsync(cancellationToken);
    await PushCatalogChangedAsync(cancellationToken);

    return Results.Json(new SavedItemView(itemId), statusCode: StatusCodes.Status201Created);
  }

  public async Task<IResult> UpdateAsync(Guid itemId,
                                         SaveItemRequest request,
                                         CancellationToken cancellationToken)
  {
    var refusal = Validate(request);

    if (refusal is null && !await AnyStationIsActiveAsync(request.StationIds!, cancellationToken))
    {
      refusal = ItemHasNoActiveStation();
    }

    if (refusal is not null)
    {
      return refusal;
    }

    var item = await _dbContext.CatalogItems
                              .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);

    if (item is null)
    {
      return Results.NotFound();
    }

    item.Name = request.Name!;
    item.CategoryName = request.CategoryName!;
    item.PriceCents = request.PriceCents;
    item.SortOrder = request.SortOrder;
    item.ProductionMinutes = request.ProductionMinutes;

    List<ItemStationAssignment> existing = await _dbContext.ItemStationAssignments
                                                          .Where(assignment => assignment.CatalogItemId == itemId)
                                                          .ToListAsync(cancellationToken);

    _dbContext.ItemStationAssignments.RemoveRange(existing);

    foreach (var stationId in request.StationIds!)
    {
      _dbContext.ItemStationAssignments.Add(new()
                                           {
                                             Id = Guid.NewGuid(),
                                             CatalogItemId = itemId,
                                             StationId = stationId
                                           });
    }

    await _dbContext.SaveChangesAsync(cancellationToken);
    await PushCatalogChangedAsync(cancellationToken);

    return Results.Ok(new SavedItemView(itemId));
  }

  public async Task<IResult> SetAvailabilityAsync(Guid itemId,
                                                  SetAvailabilityRequest request,
                                                  CancellationToken cancellationToken)
  {
    var item = await _dbContext.CatalogItems
                              .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);

    if (item is null)
    {
      return Results.NotFound();
    }

    item.IsAvailable = request.IsAvailable;
    await _dbContext.SaveChangesAsync(cancellationToken);
    await PushCatalogChangedAsync(cancellationToken);

    return Results.Ok(new SavedItemView(itemId));
  }

  public async Task<IResult> ActivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    var item = await _dbContext.CatalogItems
                              .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);

    if (item is null)
    {
      return Results.NotFound();
    }

    List<Guid> assignedStationIds = await _dbContext.ItemStationAssignments
                                                   .AsNoTracking()
                                                   .Where(assignment => assignment.CatalogItemId == itemId)
                                                   .Select(assignment => assignment.StationId)
                                                   .ToListAsync(cancellationToken);

    if (!await AnyStationIsActiveAsync(assignedStationIds, cancellationToken))
    {
      return ItemHasNoActiveStation();
    }

    item.IsActive = true;
    await _dbContext.SaveChangesAsync(cancellationToken);
    await PushCatalogChangedAsync(cancellationToken);

    return Results.Ok(new SavedItemView(itemId));
  }

  public async Task<IResult> DeactivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    var item = await _dbContext.CatalogItems
                              .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);

    if (item is null)
    {
      return Results.NotFound();
    }

    item.IsActive = false;
    await _dbContext.SaveChangesAsync(cancellationToken);
    await PushCatalogChangedAsync(cancellationToken);

    return Results.Ok(new SavedItemView(itemId));
  }

  private async Task<bool> AnyStationIsActiveAsync(IReadOnlyCollection<Guid> stationIds,
                                                   CancellationToken cancellationToken)
  {
    return await _dbContext.Stations
                          .AsNoTracking()
                          .AnyAsync(station => station.IsActive && stationIds.Contains(station.Id),
                                    cancellationToken);
  }

  private IResult ItemHasNoActiveStation()
  {
    return _resultEnvelope.Problem(StatusCodes.Status422UnprocessableEntity,
                                  "UnprocessableEntity",
                                  "admin.itemHasNoActiveStation");
  }

  private IResult? Validate(SaveItemRequest request)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                    "ValidationFailed",
                                    "admin.itemNameMissing");
    }

    if (string.IsNullOrWhiteSpace(request.CategoryName))
    {
      return _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                    "ValidationFailed",
                                    "admin.itemCategoryMissing");
    }

    if (request.StationIds is null || request.StationIds.Count == 0)
    {
      return _resultEnvelope.Problem(StatusCodes.Status422UnprocessableEntity,
                                    "UnprocessableEntity",
                                    "admin.itemNeedsAStation");
    }

    if (request.ProductionMinutes is < ShortestProductionMinutes or > LongestProductionMinutes)
    {
      return _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                    "ValidationFailed",
                                    "catalog.productionMinutesOutOfRange");
    }

    return null;
  }

  private async Task PushCatalogChangedAsync(CancellationToken cancellationToken)
  {
    var catalog = await _catalogReader.ReadAsync(_dbContext, cancellationToken);
    await _dispatcher.PushCatalogChangedAsync(catalog.Version, cancellationToken);
  }
}

public sealed record SavedItemView(Guid ItemId);
