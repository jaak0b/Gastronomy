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
                 async (Guid? festivalId,
                        AdminItemHandler handler,
                        CancellationToken cancellationToken) => await handler.ListAsync(festivalId, cancellationToken));

    group.MapPost(string.Empty,
                  async (SaveItemRequest request,
                         AdminItemHandler handler,
                         CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken));

    group.MapPut("/{itemId:guid}",
                 async (Guid itemId,
                        SaveItemRequest request,
                        AdminItemHandler handler,
                        CancellationToken cancellationToken) => await handler.UpdateAsync(itemId, request, cancellationToken));

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

  public async Task<IResult> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    if (festivalId is { } askedFestivalId
        && !await _dbContext.Festivals
                            .AsNoTracking()
                            .AnyAsync(candidate => candidate.Id == askedFestivalId, cancellationToken))
    {
      return Results.NotFound();
    }

    List<CatalogItem> items = await _dbContext.CatalogItems
                                             .AsNoTracking()
                                             .OrderBy(item => item.SortOrder)
                                             .ToListAsync(cancellationToken);

    Dictionary<Guid, FestivalCatalogItem> menuRows = festivalId is null
                                                       ? []
                                                       : await _dbContext.FestivalCatalogItems
                                                                         .AsNoTracking()
                                                                         .Where(menuRow => menuRow.FestivalId == festivalId.Value)
                                                                         .ToDictionaryAsync(menuRow => menuRow.CatalogItemId,
                                                                                            cancellationToken);

    List<ItemStationAssignment> assignments = festivalId is null
                                                ? []
                                                : await _dbContext.ItemStationAssignments
                                                                  .AsNoTracking()
                                                                  .Where(assignment => assignment.FestivalId == festivalId.Value)
                                                                  .ToListAsync(cancellationToken);

    List<AdminItemView> views =
    [
      .. items.Select(item => new AdminItemView(item.Id,
                                                item.Name,
                                                item.CategoryId,
                                                item.SortOrder,
                                                item.IsActive,
                                                item.ProductionMinutes,
                                                AtTheFestivalOf(item.Id, menuRows, assignments)))
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

  private AdminItemAtFestivalView? AtTheFestivalOf(Guid itemId,
                                                   Dictionary<Guid, FestivalCatalogItem> menuRows,
                                                   IReadOnlyCollection<ItemStationAssignment> assignments)
  {
    if (!menuRows.TryGetValue(itemId, out var menuRow))
    {
      return null;
    }

    return new(menuRow.PriceCents,
               menuRow.IsAvailable,
               [
                 .. assignments.Where(assignment => assignment.CatalogItemId == itemId)
                               .Select(assignment => assignment.StationId)
               ]);
  }

  private async Task<CatalogWrite> CreatedAsync(SaveItemRequest request, CancellationToken cancellationToken)
  {
    var refusal = Validate(request) ?? await CategoryRefusalAsync(request.CategoryId, true, cancellationToken);

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
                                 SortOrder = request.SortOrder,
                                 IsActive = true,
                                 ProductionMinutes = request.ProductionMinutes
                               });

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

    if (refusal is not null)
    {
      return new(refusal, false);
    }

    item.Name = request.Name!;
    item.CategoryId = request.CategoryId!.Value;
    item.SortOrder = request.SortOrder;
    item.ProductionMinutes = request.ProductionMinutes;

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

  private IResult? Validate(SaveItemRequest request)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                    "ValidationFailed",
                                    "admin.itemNameMissing");
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
