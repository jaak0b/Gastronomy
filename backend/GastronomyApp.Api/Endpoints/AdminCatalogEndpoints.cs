using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
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

  private readonly GastronomyAppDbContext _dbContext;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly CatalogWriteTransaction _writeTransaction;

  public AdminItemHandler(GastronomyAppDbContext dbContext,
                          CatalogWriteTransaction writeTransaction,
                          ResultEnvelope resultEnvelope)
  {
    _dbContext = dbContext;
    _writeTransaction = writeTransaction;
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
                                                item.CategoryId,
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

  public Task<IResult> CreateAsync(SaveItemRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return _writeTransaction.RunAsync(_dbContext,
                                      transactionCancellationToken =>
                                        CreatedAsync(request, transactionCancellationToken),
                                      cancellationToken);
  }

  public Task<IResult> UpdateAsync(Guid itemId,
                                   SaveItemRequest request,
                                   CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return _writeTransaction.RunAsync(_dbContext,
                                      transactionCancellationToken =>
                                        UpdatedAsync(itemId, request, transactionCancellationToken),
                                      cancellationToken);
  }

  public Task<IResult> SetAvailabilityAsync(Guid itemId,
                                            SetAvailabilityRequest request,
                                            CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return _writeTransaction.RunAsync(_dbContext,
                                      transactionCancellationToken =>
                                        AvailabilitySetAsync(itemId, request, transactionCancellationToken),
                                      cancellationToken);
  }

  public Task<IResult> ActivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    return _writeTransaction.RunAsync(_dbContext,
                                      transactionCancellationToken =>
                                        SwitchedOnAsync(itemId, transactionCancellationToken),
                                      cancellationToken);
  }

  public Task<IResult> DeactivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    return _writeTransaction.RunAsync(_dbContext,
                                      transactionCancellationToken =>
                                        SwitchedOffAsync(itemId, transactionCancellationToken),
                                      cancellationToken);
  }

  private async Task<CatalogWrite> CreatedAsync(SaveItemRequest request, CancellationToken cancellationToken)
  {
    var refusal = Validate(request) ?? await CategoryRefusalAsync(request.CategoryId, true, cancellationToken);

    if (refusal is null && !await AnyStationIsActiveAsync(request.StationIds!, cancellationToken))
    {
      refusal = ItemHasNoActiveStation();
    }

    if (refusal is not null)
    {
      return new(refusal, false);
    }

    var itemId = Guid.NewGuid();

    _dbContext.CatalogItems.Add(new()
                               {
                                 Id = itemId,
                                 Name = request.Name!,
                                 CategoryId = request.CategoryId!.Value,
                                 PriceCents = request.PriceCents,
                                 SortOrder = request.SortOrder,
                                 IsActive = true,
                                 IsAvailable = true,
                                 ProductionMinutes = request.ProductionMinutes
                               });

    AssignStations(itemId, request.StationIds!);

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Json(new SavedItemView(itemId), statusCode: StatusCodes.Status201Created), true);
  }

  private async Task<CatalogWrite> UpdatedAsync(Guid itemId,
                                                SaveItemRequest request,
                                                CancellationToken cancellationToken)
  {
    var item = await _dbContext.CatalogItems
                              .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);

    if (item is null)
    {
      return new(Results.NotFound(), false);
    }

    var refusal = Validate(request)
                  ?? await CategoryRefusalAsync(request.CategoryId, item.IsActive, cancellationToken);

    if (refusal is null && !await AnyStationIsActiveAsync(request.StationIds!, cancellationToken))
    {
      refusal = ItemHasNoActiveStation();
    }

    if (refusal is not null)
    {
      return new(refusal, false);
    }

    item.Name = request.Name!;
    item.CategoryId = request.CategoryId!.Value;
    item.PriceCents = request.PriceCents;
    item.SortOrder = request.SortOrder;
    item.ProductionMinutes = request.ProductionMinutes;

    List<ItemStationAssignment> existing = await _dbContext.ItemStationAssignments
                                                          .Where(assignment => assignment.CatalogItemId == itemId)
                                                          .ToListAsync(cancellationToken);

    _dbContext.ItemStationAssignments.RemoveRange(existing);
    AssignStations(itemId, request.StationIds!);

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Ok(new SavedItemView(itemId)), true);
  }

  private async Task<CatalogWrite> AvailabilitySetAsync(Guid itemId,
                                                        SetAvailabilityRequest request,
                                                        CancellationToken cancellationToken)
  {
    var item = await _dbContext.CatalogItems
                              .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);

    if (item is null)
    {
      return new(Results.NotFound(), false);
    }

    if (item.IsAvailable == request.IsAvailable)
    {
      return new(Results.Ok(new SavedItemView(itemId)), false);
    }

    item.IsAvailable = request.IsAvailable;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Ok(new SavedItemView(itemId)), true);
  }

  private async Task<CatalogWrite> SwitchedOnAsync(Guid itemId, CancellationToken cancellationToken)
  {
    var item = await _dbContext.CatalogItems
                              .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);

    if (item is null)
    {
      return new(Results.NotFound(), false);
    }

    List<Guid> assignedStationIds = await _dbContext.ItemStationAssignments
                                                   .AsNoTracking()
                                                   .Where(assignment => assignment.CatalogItemId == itemId)
                                                   .Select(assignment => assignment.StationId)
                                                   .ToListAsync(cancellationToken);

    if (!await AnyStationIsActiveAsync(assignedStationIds, cancellationToken))
    {
      return new(ItemHasNoActiveStation(), false);
    }

    var categoryRefusal = await CategoryRefusalAsync(item.CategoryId, true, cancellationToken);

    if (categoryRefusal is not null)
    {
      return new(categoryRefusal, false);
    }

    item.IsActive = true;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Ok(new SavedItemView(itemId)), true);
  }

  private async Task<CatalogWrite> SwitchedOffAsync(Guid itemId, CancellationToken cancellationToken)
  {
    var item = await _dbContext.CatalogItems
                              .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);

    if (item is null)
    {
      return new(Results.NotFound(), false);
    }

    item.IsActive = false;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Ok(new SavedItemView(itemId)), true);
  }

  private void AssignStations(Guid itemId, IReadOnlyCollection<Guid> stationIds)
  {
    foreach (var stationId in stationIds)
    {
      _dbContext.ItemStationAssignments.Add(new()
                                           {
                                             Id = Guid.NewGuid(),
                                             CatalogItemId = itemId,
                                             StationId = stationId
                                           });
    }
  }

  private async Task<IResult?> CategoryRefusalAsync(Guid? categoryId,
                                                    bool theArticleIsSwitchedOn,
                                                    CancellationToken cancellationToken)
  {
    var category = categoryId is null
                     ? null
                     : await _dbContext.CatalogCategories
                                       .AsNoTracking()
                                       .FirstOrDefaultAsync(candidate => candidate.Id == categoryId.Value,
                                                            cancellationToken);

    if (category is null)
    {
      return _resultEnvelope.Problem(StatusCodes.Status422UnprocessableEntity,
                                    "UnprocessableEntity",
                                    "admin.itemCategoryUnknown");
    }

    return category.IsActive || !theArticleIsSwitchedOn
             ? null
             : _resultEnvelope.Problem(StatusCodes.Status422UnprocessableEntity,
                                       "UnprocessableEntity",
                                       "admin.itemCategoryIsOff");
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
}

public sealed record SavedItemView(Guid ItemId);
