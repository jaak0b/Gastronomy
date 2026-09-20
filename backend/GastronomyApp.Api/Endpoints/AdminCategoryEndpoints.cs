using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class AdminCategoryEndpoints
{
  public static IEndpointRouteBuilder MapAdminCategoryEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/categories");

    group.MapGet(string.Empty,
                 async (AdminCategoryHandler handler,
                        CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    group.MapPost(string.Empty,
                  async (SaveCategoryRequest request,
                         AdminCategoryHandler handler,
                         CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken));

    group.MapPut("/{categoryId:guid}",
                 async (Guid categoryId,
                        SaveCategoryRequest request,
                        AdminCategoryHandler handler,
                        CancellationToken cancellationToken) =>
                   await handler.UpdateAsync(categoryId, request, cancellationToken));

    group.MapPost("/{categoryId:guid}/move",
                  async (Guid categoryId,
                         MoveCategoryRequest request,
                         AdminCategoryHandler handler,
                         CancellationToken cancellationToken) =>
                    await handler.MoveAsync(categoryId, request, cancellationToken));

    group.MapPost("/{categoryId:guid}/activate",
                  async (Guid categoryId,
                         AdminCategoryHandler handler,
                         CancellationToken cancellationToken) => await handler.ActivateAsync(categoryId, cancellationToken));

    group.MapPost("/{categoryId:guid}/deactivate",
                  async (Guid categoryId,
                         AdminCategoryHandler handler,
                         CancellationToken cancellationToken) =>
                    await handler.DeactivateAsync(categoryId, cancellationToken));

    return routes;
  }
}

public sealed class AdminCategoryHandler
{
  private readonly ColorFormatValidator _colour;
  private readonly GastronomyAppDbContext _dbContext;
  private readonly CatalogCategoryOrdering _ordering;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly CatalogWriteTransaction _writeTransaction;

  public AdminCategoryHandler(GastronomyAppDbContext dbContext,
                              ColorFormatValidator colour,
                              CatalogCategoryOrdering ordering,
                              CatalogWriteTransaction writeTransaction,
                              ResultEnvelope resultEnvelope)
  {
    _dbContext = dbContext;
    _colour = colour;
    _ordering = ordering;
    _writeTransaction = writeTransaction;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    List<CatalogCategory> categories = await _dbContext.CatalogCategories
                                                       .AsNoTracking()
                                                       .OrderBy(category => category.SortOrder)
                                                       .ToListAsync(cancellationToken);

    return Results.Ok(BuildCategoryListView(categories));
  }

  public Task<IResult> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    return _writeTransaction.RunAsync(_dbContext, transactionCancellationToken => CreatedAsync(request, transactionCancellationToken), cancellationToken);
  }

  public Task<IResult> UpdateAsync(Guid categoryId, SaveCategoryRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    return _writeTransaction.RunAsync(_dbContext, transactionCancellationToken => UpdatedAsync(categoryId, request, transactionCancellationToken), cancellationToken);
  }

  public Task<IResult> MoveAsync(Guid categoryId, MoveCategoryRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return _writeTransaction.RunAsync(_dbContext, transactionCancellationToken => MovedAsync(categoryId, request, transactionCancellationToken), cancellationToken);
  }

  public Task<IResult> ActivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    return _writeTransaction.RunAsync(_dbContext, transactionCancellationToken => SwitchedOnAsync(categoryId, transactionCancellationToken), cancellationToken);
  }

  public Task<IResult> DeactivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    return _writeTransaction.RunAsync(_dbContext, transactionCancellationToken => SwitchedOffAsync(categoryId, transactionCancellationToken), cancellationToken);
  }

  private async Task<CatalogWrite> CreatedAsync(SaveCategoryRequest request, CancellationToken cancellationToken)
  {
    List<CatalogCategory> categories = await _dbContext.CatalogCategories
                                                       .OrderBy(category => category.SortOrder)
                                                       .ToListAsync(cancellationToken);;
    var refusal = ValidateRequest(request, categories, null);

    if (refusal is not null)
      return new(refusal, false);

    CatalogCategory created = new()
                              {
                                Id = Guid.NewGuid(),
                                Name = request.Name?.Trim()!,
                                ColourHex = request.ColourHex!,
                                SortOrder = _ordering.NextSortOrder(categories.Select(category => category.SortOrder).ToList()),
                                IsActive = true
                              };

    _dbContext.CatalogCategories.Add(created);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Json(BuildCategoryView(created), statusCode: StatusCodes.Status201Created), true);
  }

  private async Task<CatalogWrite> UpdatedAsync(Guid categoryId, SaveCategoryRequest request, CancellationToken cancellationToken)
  {
    List<CatalogCategory> categories = await _dbContext.CatalogCategories
                                                       .OrderBy(category => category.SortOrder)
                                                       .ToListAsync(cancellationToken);
    var category = categories.FirstOrDefault(candidate => candidate.Id == categoryId);

    if (category is null)
      return new(Results.NotFound(), false);

    var refusal = ValidateRequest(request, categories, categoryId);

    if (refusal is not null)
      return new(refusal, false);

    category.Name = request.Name?.Trim()!;
    category.ColourHex = request.ColourHex!;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Ok(BuildCategoryView(category)), true);
  }

  private async Task<CatalogWrite> MovedAsync(Guid categoryId,
                                              MoveCategoryRequest request,
                                              CancellationToken cancellationToken)
  {
    List<CatalogCategory> categories = await _dbContext.CatalogCategories
                                                       .OrderBy(category => category.SortOrder)
                                                       .ToListAsync(cancellationToken);

    if (categories.All(candidate => candidate.Id != categoryId))
    {
      return new(Results.NotFound(), false);
    }

    Dictionary<Guid, CatalogCategory> categoriesById = categories.ToDictionary(category => category.Id);

    IReadOnlyList<CatalogCategoryPosition> positions = _ordering.Move(categories.Select(category => category.Id).ToList(), categoryId, request.Direction);

    List<CatalogCategory> reordered = positions.Select(position => categoriesById[position.CategoryId]).ToList();

    if (positions.All(position => categoriesById[position.CategoryId].SortOrder == position.SortOrder))
    {
      return new(Results.Ok(BuildCategoryListView(reordered)), false);
    }

    foreach (var position in positions)
    {
      categoriesById[position.CategoryId].SortOrder = position.SortOrder;
    }

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Ok(BuildCategoryListView(reordered)), true);
  }

  private async Task<CatalogWrite> SwitchedOnAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    var category = await _dbContext.CatalogCategories
                                   .FirstOrDefaultAsync(candidate => candidate.Id == categoryId, cancellationToken);

    if (category is null)
    {
      return new(Results.NotFound(), false);
    }

    category.IsActive = true;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Ok(BuildCategoryView(category)), true);
  }

  private async Task<CatalogWrite> SwitchedOffAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    var category = await _dbContext.CatalogCategories
                                   .FirstOrDefaultAsync(candidate => candidate.Id == categoryId, cancellationToken);

    if (category is null)
    {
      return new(Results.NotFound(), false);
    }

    var holdsActiveItems = await _dbContext.CatalogItems
                                           .AnyAsync(item => item.CategoryId == categoryId && item.IsActive,
                                                     cancellationToken);

    if (holdsActiveItems)
    {
      return new(_resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                         "CategoryHasActiveItems",
                                         "admin.categoryHasActiveItems"),
                 false);
    }

    category.IsActive = false;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(Results.Ok(BuildCategoryView(category)), true);
  }

  private IResult? ValidateRequest(SaveCategoryRequest request, IReadOnlyCollection<CatalogCategory> categories, Guid? categoryBeingSaved)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "admin.categoryNameMissing");
    }

    if (!_colour.IsWellFormed(request.ColourHex))
    {
      return _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "admin.categoryColourInvalid");
    }

    var wantedName = request.Name.Trim();
    var taken = categories.Any(category => category.Id != categoryBeingSaved && string.Equals(category.Name, wantedName, StringComparison.OrdinalIgnoreCase));

    if (taken)
      return _resultEnvelope.Problem(StatusCodes.Status409Conflict, "CategoryNameTaken", "admin.categoryNameTaken");
    return null;
  }

  private AdminCategoryListView BuildCategoryListView(IReadOnlyCollection<CatalogCategory> categories)
  {
    return new([.. categories.Select(BuildCategoryView)]);
  }

  private AdminCategoryView BuildCategoryView(CatalogCategory category)
  {
    return new(category.Id, category.Name, category.ColourHex, category.SortOrder, category.IsActive);
  }
}
