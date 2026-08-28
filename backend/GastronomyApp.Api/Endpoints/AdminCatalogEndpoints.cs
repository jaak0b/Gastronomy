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
    RouteGroupBuilder group = routes.MapGroup("/api/admin/items");

    group.MapGet(string.Empty, async (
        AdminItemHandler handler,
        CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    group.MapPost(string.Empty, async (
        SaveItemRequest request,
        AdminItemHandler handler,
        CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken));

    group.MapPut("/{itemId:guid}", async (
        Guid itemId,
        SaveItemRequest request,
        AdminItemHandler handler,
        CancellationToken cancellationToken) => await handler.UpdateAsync(itemId, request, cancellationToken));

    group.MapPost("/{itemId:guid}/availability", async (
        Guid itemId,
        SetAvailabilityRequest request,
        AdminItemHandler handler,
        CancellationToken cancellationToken) =>
            await handler.SetAvailabilityAsync(itemId, request, cancellationToken));

    group.MapPost("/{itemId:guid}/activate", async (
        Guid itemId,
        AdminItemHandler handler,
        CancellationToken cancellationToken) => await handler.ActivateAsync(itemId, cancellationToken));

    group.MapPost("/{itemId:guid}/deactivate", async (
        Guid itemId,
        AdminItemHandler handler,
        CancellationToken cancellationToken) => await handler.DeactivateAsync(itemId, cancellationToken));

    return routes;
  }
}

public sealed class AdminItemHandler
{
  private readonly GastronomyAppDbContext dbContext;
  private readonly CatalogReader catalogReader;
  private readonly HubNotificationDispatcher dispatcher;
  private readonly ResultEnvelope resultEnvelope;

  public AdminItemHandler(
      GastronomyAppDbContext dbContext,
      CatalogReader catalogReader,
      HubNotificationDispatcher dispatcher,
      ResultEnvelope resultEnvelope)
  {
    this.dbContext = dbContext;
    this.catalogReader = catalogReader;
    this.dispatcher = dispatcher;
    this.resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    List<CatalogItem> items = await dbContext.CatalogItems
        .AsNoTracking()
        .OrderBy(item => item.SortOrder)
        .ToListAsync(cancellationToken);

    List<ItemStationAssignment> assignments = await dbContext.ItemStationAssignments
        .AsNoTracking()
        .ToListAsync(cancellationToken);

    List<AdminItemView> views =
    [
        .. items.Select(item => new AdminItemView(
                item.Id,
                item.Name,
                item.CategoryName,
                item.PriceCents,
                item.SortOrder,
                item.IsActive,
                item.IsAvailable,
                [
                    .. assignments
                        .Where(assignment => assignment.CatalogItemId == item.Id)
                        .Select(assignment => assignment.StationId),
                ])),
        ];

    return Results.Ok(new AdminItemListView(views));
  }

  public async Task<IResult> CreateAsync(SaveItemRequest request, CancellationToken cancellationToken)
  {
    IResult? refusal = Validate(request);

    if (refusal is null && !await AnyStationIsActiveAsync(request.StationIds!, cancellationToken))
    {
      refusal = ItemHasNoActiveStation();
    }

    if (refusal is not null)
    {
      return refusal;
    }

    Guid itemId = Guid.NewGuid();

    dbContext.CatalogItems.Add(new CatalogItem
    {
      Id = itemId,
      Name = request.Name!,
      CategoryName = request.CategoryName!,
      PriceCents = request.PriceCents,
      SortOrder = request.SortOrder,
      IsActive = true,
      IsAvailable = true,
    });

    foreach (Guid stationId in request.StationIds!)
    {
      dbContext.ItemStationAssignments.Add(new ItemStationAssignment
      {
        Id = Guid.NewGuid(),
        CatalogItemId = itemId,
        StationId = stationId,
      });
    }

    await dbContext.SaveChangesAsync(cancellationToken);
    await PushCatalogChangedAsync(cancellationToken);

    return Results.Json(new SavedItemView(itemId), statusCode: StatusCodes.Status201Created);
  }

  public async Task<IResult> UpdateAsync(
      Guid itemId,
      SaveItemRequest request,
      CancellationToken cancellationToken)
  {
    IResult? refusal = Validate(request);

    if (refusal is null && !await AnyStationIsActiveAsync(request.StationIds!, cancellationToken))
    {
      refusal = ItemHasNoActiveStation();
    }

    if (refusal is not null)
    {
      return refusal;
    }

    CatalogItem? item = await dbContext.CatalogItems
        .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);

    if (item is null)
    {
      return Results.NotFound();
    }

    item.Name = request.Name!;
    item.CategoryName = request.CategoryName!;
    item.PriceCents = request.PriceCents;
    item.SortOrder = request.SortOrder;

    List<ItemStationAssignment> existing = await dbContext.ItemStationAssignments
        .Where(assignment => assignment.CatalogItemId == itemId)
        .ToListAsync(cancellationToken);

    dbContext.ItemStationAssignments.RemoveRange(existing);

    foreach (Guid stationId in request.StationIds!)
    {
      dbContext.ItemStationAssignments.Add(new ItemStationAssignment
      {
        Id = Guid.NewGuid(),
        CatalogItemId = itemId,
        StationId = stationId,
      });
    }

    await dbContext.SaveChangesAsync(cancellationToken);
    await PushCatalogChangedAsync(cancellationToken);

    return Results.Ok(new SavedItemView(itemId));
  }

  public async Task<IResult> SetAvailabilityAsync(
      Guid itemId,
      SetAvailabilityRequest request,
      CancellationToken cancellationToken)
  {
    CatalogItem? item = await dbContext.CatalogItems
        .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);

    if (item is null)
    {
      return Results.NotFound();
    }

    item.IsAvailable = request.IsAvailable;
    await dbContext.SaveChangesAsync(cancellationToken);
    await PushCatalogChangedAsync(cancellationToken);

    return Results.Ok(new SavedItemView(itemId));
  }

  public async Task<IResult> ActivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    CatalogItem? item = await dbContext.CatalogItems
        .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);

    if (item is null)
    {
      return Results.NotFound();
    }

    List<Guid> assignedStationIds = await dbContext.ItemStationAssignments
        .AsNoTracking()
        .Where(assignment => assignment.CatalogItemId == itemId)
        .Select(assignment => assignment.StationId)
        .ToListAsync(cancellationToken);

    if (!await AnyStationIsActiveAsync(assignedStationIds, cancellationToken))
    {
      return ItemHasNoActiveStation();
    }

    item.IsActive = true;
    await dbContext.SaveChangesAsync(cancellationToken);
    await PushCatalogChangedAsync(cancellationToken);

    return Results.Ok(new SavedItemView(itemId));
  }

  public async Task<IResult> DeactivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    CatalogItem? item = await dbContext.CatalogItems
        .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);

    if (item is null)
    {
      return Results.NotFound();
    }

    item.IsActive = false;
    await dbContext.SaveChangesAsync(cancellationToken);
    await PushCatalogChangedAsync(cancellationToken);

    return Results.Ok(new SavedItemView(itemId));
  }

  private async Task<bool> AnyStationIsActiveAsync(
      IReadOnlyCollection<Guid> stationIds,
      CancellationToken cancellationToken)
  {
    return await dbContext.Stations
        .AsNoTracking()
        .AnyAsync(
            station => station.IsActive && stationIds.Contains(station.Id),
            cancellationToken);
  }

  private IResult ItemHasNoActiveStation()
  {
    return resultEnvelope.Problem(
        StatusCodes.Status422UnprocessableEntity,
        "UnprocessableEntity",
        "admin.itemHasNoActiveStation");
  }

  private IResult? Validate(SaveItemRequest request)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return resultEnvelope.Problem(
          StatusCodes.Status400BadRequest,
          "ValidationFailed",
          "admin.itemNameMissing");
    }

    if (string.IsNullOrWhiteSpace(request.CategoryName))
    {
      return resultEnvelope.Problem(
          StatusCodes.Status400BadRequest,
          "ValidationFailed",
          "admin.itemCategoryMissing");
    }

    if (request.StationIds is null || request.StationIds.Count == 0)
    {
      return resultEnvelope.Problem(
          StatusCodes.Status422UnprocessableEntity,
          "UnprocessableEntity",
          "admin.itemNeedsAStation");
    }

    return null;
  }

  private async Task PushCatalogChangedAsync(CancellationToken cancellationToken)
  {
    CatalogView catalog = await catalogReader.ReadAsync(dbContext, cancellationToken);
    await dispatcher.PushCatalogChangedAsync(catalog.Version, cancellationToken);
  }
}

public sealed record SavedItemView(Guid ItemId);
