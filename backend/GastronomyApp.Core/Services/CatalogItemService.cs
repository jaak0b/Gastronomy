using ErrorOr;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Refusals;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class CatalogItemService
{
  private const double ShortestProductionMinutes = 0;
  private const double LongestProductionMinutes = 600;
  private readonly IGastronomyDatabase _database;
  private readonly TypeAdapterConfig _mapping;
  private readonly RunningFestivalLookup _runningFestival;

  public CatalogItemService(IGastronomyDatabase database, TypeAdapterConfig mapping, RunningFestivalLookup runningFestival)
  {
    _database = database;
    _mapping = mapping;
    _runningFestival = runningFestival;
  }

  public async Task<ErrorOr<AdminItemListView>> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    if (festivalId is { } askedFestivalId && !await _database.Festivals.AsNoTracking().AnyAsync(festival => festival.Id == askedFestivalId, cancellationToken))
      return Refusal.CatalogItem.FestivalNotFound(askedFestivalId);

    var items = await _database.CatalogItems.AsNoTracking().OrderBy(item => item.SortOrder).ProjectToType<AdminItemView>(_mapping).ToListAsync(cancellationToken);

    if (festivalId is not { } menuFestivalId)
      return new AdminItemListView(items);

    var menu = await _database.FestivalCatalogItems.AsNoTracking().Where(menuRow => menuRow.FestivalId == menuFestivalId).ProjectToType<ItemMenuEntry>(_mapping).ToDictionaryAsync(entry => entry.CatalogItemId, entry => entry.AtTheFestival, cancellationToken);

    return new AdminItemListView(items.Select(item => item with { AtTheFestival = menu.GetValueOrDefault(item.ItemId) }).ToList());
  }

  public async Task<ErrorOr<AdminItemView>> CreateAsync(SaveItemRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Error? refusal = ProductionMinutesRefusal(request.ProductionMinutes) ?? await NameRefusalAsync(request.Name!, null, cancellationToken) ?? await CategoryRefusalAsync(request.CategoryId, true, cancellationToken);

    if (refusal is { } error)
      return error;

    CatalogItem created = new()
                          {
                            Id = Guid.NewGuid(),
                            Name = request.Name!,
                            CategoryId = request.CategoryId!.Value,
                            SortOrder = request.SortOrder,
                            IsActive = true,
                            ProductionMinutes = request.ProductionMinutes,
                            IsQueueIndependent = request.IsQueueIndependent
                          };

    _database.CatalogItems.Add(created);
    await _database.SaveChangesAsync(cancellationToken);

    return created.Adapt<AdminItemView>(_mapping);
  }

  public async Task<ErrorOr<SavedItemView>> UpdateAsync(Guid itemId, SaveItemRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var item = await _database.CatalogItems.FirstOrDefaultAsync(row => row.Id == itemId, cancellationToken);

    if (item is null)
      return Refusal.CatalogItem.ItemNotFound(itemId);

    Error? refusal = ProductionMinutesRefusal(request.ProductionMinutes) ?? await NameRefusalAsync(request.Name!, itemId, cancellationToken) ?? await CategoryRefusalAsync(request.CategoryId, item.IsActive, cancellationToken);

    if (refusal is { } error)
      return error;

    item.Name = request.Name!;
    item.CategoryId = request.CategoryId!.Value;
    item.SortOrder = request.SortOrder;
    item.ProductionMinutes = request.ProductionMinutes;
    item.IsQueueIndependent = request.IsQueueIndependent;

    await _database.SaveChangesAsync(cancellationToken);

    return item.Adapt<SavedItemView>(_mapping);
  }

  public async Task<ErrorOr<SavedItemView>> ActivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    var item = await _database.CatalogItems.FirstOrDefaultAsync(row => row.Id == itemId, cancellationToken);

    if (item is null)
      return Refusal.CatalogItem.ItemNotFound(itemId);

    if (await CategoryRefusalAsync(item.CategoryId, true, cancellationToken) is { } categoryRefusal)
      return categoryRefusal;

    item.IsActive = true;
    await _database.SaveChangesAsync(cancellationToken);

    return item.Adapt<SavedItemView>(_mapping);
  }

  public async Task<ErrorOr<SavedItemView>> DeactivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    var item = await _database.CatalogItems.FirstOrDefaultAsync(row => row.Id == itemId, cancellationToken);

    if (item is null)
      return Refusal.CatalogItem.ItemNotFound(itemId);

    var runningFestival = await _runningFestival.FindAsync(cancellationToken);

    if (runningFestival is not null && await _database.FestivalCatalogItems.AsNoTracking().AnyAsync(menuRow => menuRow.FestivalId == runningFestival.Id && menuRow.CatalogItemId == itemId, cancellationToken))
      return Refusal.CatalogItem.ItemIsOnTheRunningFestivalsMenu(itemId);

    item.IsActive = false;
    await _database.SaveChangesAsync(cancellationToken);

    return item.Adapt<SavedItemView>(_mapping);
  }

  public async Task<ErrorOr<SavedItemView>> SetRecipeLineAsync(Guid itemId, Guid ingredientId, SaveItemIngredientRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (!await _database.CatalogItems.AsNoTracking().AnyAsync(item => item.Id == itemId, cancellationToken))
      return Refusal.Ingredients.ItemNotFound(itemId);

    if (!await _database.Ingredients.AsNoTracking().AnyAsync(ingredient => ingredient.Id == ingredientId, cancellationToken))
      return Refusal.Ingredients.IngredientNotFound(ingredientId);

    if (request.Amount is not { } usedPerUnit || !double.IsFinite(usedPerUnit) || usedPerUnit <= 0)
      return Refusal.Ingredients.AmountInvalid(request.Amount ?? double.NaN);

    var recipeLine = await FindRecipeLineAsync(itemId, ingredientId, cancellationToken);

    if (recipeLine is null)
    {
      recipeLine = new()
                   {
                     Id = Guid.NewGuid(),
                     CatalogItemId = itemId,
                     IngredientId = ingredientId,
                     Amount = usedPerUnit
                   };

      _database.CatalogItemIngredients.Add(recipeLine);
    }
    else
      recipeLine.Amount = usedPerUnit;

    await _database.SaveChangesAsync(cancellationToken);

    return recipeLine.Adapt<SavedItemView>(_mapping);
  }

  public async Task<ErrorOr<Success>> RemoveRecipeLineAsync(Guid itemId, Guid ingredientId, CancellationToken cancellationToken)
  {
    var recipeLine = await FindRecipeLineAsync(itemId, ingredientId, cancellationToken);

    if (recipeLine is null)
      return Refusal.Ingredients.RecipeLineNotFound(itemId, ingredientId);

    _database.CatalogItemIngredients.Remove(recipeLine);
    await _database.SaveChangesAsync(cancellationToken);

    return Result.Success;
  }

  private async Task<CatalogItemIngredient?> FindRecipeLineAsync(Guid itemId, Guid ingredientId, CancellationToken cancellationToken)
  {
    return await _database.CatalogItemIngredients.FirstOrDefaultAsync(recipeLine => recipeLine.CatalogItemId == itemId && recipeLine.IngredientId == ingredientId, cancellationToken);
  }

  private async Task<Error?> NameRefusalAsync(string name, Guid? itemKeepingItsOwnName, CancellationToken cancellationToken)
  {
    if (!await _database.CatalogItems.AsNoTracking().Namesakes(name, itemKeepingItsOwnName).AnyAsync(cancellationToken))
      return null;

    return Refusal.CatalogItem.NameTaken(name);
  }

  private async Task<Error?> CategoryRefusalAsync(Guid? categoryId, bool theArticleIsSwitchedOn, CancellationToken cancellationToken)
  {
    CatalogCategory? category = null;

    if (categoryId is { } askedCategoryId)
      category = await _database.CatalogCategories.AsNoTracking().FirstOrDefaultAsync(row => row.Id == askedCategoryId, cancellationToken);

    if (category is null)
      return Refusal.CatalogItem.CategoryUnknown(categoryId);

    if (category.IsActive || !theArticleIsSwitchedOn)
      return null;

    return Refusal.CatalogItem.CategoryIsSwitchedOff(category.Id);
  }

  private Error? ProductionMinutesRefusal(double? productionMinutes)
  {
    if (productionMinutes is { } minutes && (minutes is < ShortestProductionMinutes or > LongestProductionMinutes || Math.Round(minutes, 1) != minutes))
      return Refusal.CatalogItem.ProductionMinutesOutOfRange(minutes);

    return null;
  }
}
