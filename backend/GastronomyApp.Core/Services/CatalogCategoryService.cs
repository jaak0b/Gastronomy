using ErrorOr;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Refusals;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class CatalogCategoryService
{
  private readonly IGastronomyDatabase _database;
  private readonly TypeAdapterConfig _mapping;
  private readonly SortOrdering _ordering;

  public CatalogCategoryService(IGastronomyDatabase database, TypeAdapterConfig mapping, SortOrdering ordering)
  {
    _database = database;
    _mapping = mapping;
    _ordering = ordering;
  }

  public async Task<AdminCategoryListView> ListAsync(CancellationToken cancellationToken)
  {
    var categories = await _database.CatalogCategories.AsNoTracking().OrderBy(category => category.SortOrder).ProjectToType<AdminCategoryView>(_mapping).ToListAsync(cancellationToken);

    return new(categories);
  }

  public async Task<ErrorOr<AdminCategoryView>> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var name = request.Name!.Trim();

    if (await NameIsTakenAsync(name, null, cancellationToken))
      return Refusal.CatalogCategory.NameTaken(name);

    var takenSortOrders = await _database.CatalogCategories.AsNoTracking().Select(category => category.SortOrder).ToListAsync(cancellationToken);

    CatalogCategory created = new()
                              {
                                Id = Guid.NewGuid(),
                                Name = name,
                                ColourHex = request.ColourHex!,
                                SortOrder = _ordering.NextSortOrder(takenSortOrders),
                                IsActive = true
                              };

    _database.CatalogCategories.Add(created);
    await _database.SaveChangesAsync(cancellationToken);

    return created.Adapt<AdminCategoryView>(_mapping);
  }

  public async Task<ErrorOr<AdminCategoryView>> UpdateAsync(Guid categoryId, SaveCategoryRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var category = await _database.CatalogCategories.FirstOrDefaultAsync(row => row.Id == categoryId, cancellationToken);

    if (category is null)
      return Refusal.CatalogCategory.CategoryNotFound(categoryId);

    var name = request.Name!.Trim();

    if (await NameIsTakenAsync(name, categoryId, cancellationToken))
      return Refusal.CatalogCategory.NameTaken(name);

    category.Name = name;
    category.ColourHex = request.ColourHex!;
    await _database.SaveChangesAsync(cancellationToken);

    return category.Adapt<AdminCategoryView>(_mapping);
  }

  public async Task<ErrorOr<AdminCategoryListView>> MoveAsync(Guid categoryId, MoveDirection direction, CancellationToken cancellationToken)
  {
    IReadOnlyList<CatalogCategory> categories = await _database.CatalogCategories.OrderBy(category => category.SortOrder).ToListAsync(cancellationToken);

    if (categories.All(candidate => candidate.Id != categoryId))
      return Refusal.CatalogCategory.CategoryNotFound(categoryId);

    var reordered = _ordering.Move(categories, categoryId, direction);

    if (!_ordering.IsNumberedInOrder(reordered))
    {
      _ordering.NumberInOrder(reordered);
      await _database.SaveChangesAsync(cancellationToken);
    }

    return new AdminCategoryListView(reordered.Select(category => category.Adapt<AdminCategoryView>(_mapping)).ToList());
  }

  public async Task<ErrorOr<AdminCategoryView>> DeactivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    var category = await _database.CatalogCategories.FirstOrDefaultAsync(row => row.Id == categoryId, cancellationToken);

    if (category is null)
      return Refusal.CatalogCategory.CategoryNotFound(categoryId);

    if (await _database.CatalogItems.AsNoTracking().Active().AnyAsync(item => item.CategoryId == categoryId, cancellationToken))
      return Refusal.CatalogCategory.CategoryHoldsActiveItems(categoryId);

    category.IsActive = false;
    await _database.SaveChangesAsync(cancellationToken);

    return category.Adapt<AdminCategoryView>(_mapping);
  }

  private async Task<bool> NameIsTakenAsync(string name, Guid? ownCategoryId, CancellationToken cancellationToken)
  {
    return await _database.CatalogCategories.AsNoTracking().Namesakes(name, ownCategoryId).AnyAsync(cancellationToken);
  }
}
