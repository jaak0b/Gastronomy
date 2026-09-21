using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Services;

public sealed class CatalogItemAdministrationService
{
  private const double ShortestProductionMinutes = 0;
  private const double LongestProductionMinutes = 600;

  private readonly ICatalogCategoryRepository _categoryRepository;
  private readonly IFestivalRepository _festivalRepository;
  private readonly ICatalogItemRepository _itemRepository;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly ITransactionRunner _transactionRunner;

  public CatalogItemAdministrationService(ICatalogItemRepository itemRepository, ICatalogCategoryRepository categoryRepository, IFestivalRepository festivalRepository, RunningFestivalLookup runningFestival, ITransactionRunner transactionRunner)
  {
    _itemRepository = itemRepository;
    _categoryRepository = categoryRepository;
    _festivalRepository = festivalRepository;
    _runningFestival = runningFestival;
    _transactionRunner = transactionRunner;
  }

  public async Task<ErrorOr<IReadOnlyList<CatalogItem>>> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    if (festivalId is { } askedFestivalId && !await _festivalRepository.ExistsAsync(askedFestivalId, cancellationToken))
      return Refusal.CatalogItem.FestivalNotFound(askedFestivalId);

    IReadOnlyList<CatalogItem> items = await _itemRepository.FindAllOrderedAsync(festivalId, cancellationToken);

    return items.ToErrorOr();
  }

  public Task<ErrorOr<CatalogItem>> CreateAsync(string? name, Guid? categoryId, int sortOrder, double? productionMinutes, bool isQueueIndependent, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => CreatedAsync(name, categoryId, sortOrder, productionMinutes, isQueueIndependent, transactionCancellationToken), cancellationToken);
  }

  public Task<ErrorOr<CatalogItem>> UpdateAsync(Guid itemId, string? name, Guid? categoryId, int sortOrder, double? productionMinutes, bool isQueueIndependent, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => UpdatedAsync(itemId, name, categoryId, sortOrder, productionMinutes, isQueueIndependent, transactionCancellationToken), cancellationToken);
  }

  public Task<ErrorOr<CatalogItem>> ActivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => SwitchedOnAsync(itemId, transactionCancellationToken), cancellationToken);
  }

  public Task<ErrorOr<CatalogItem>> DeactivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => SwitchedOffAsync(itemId, transactionCancellationToken), cancellationToken);
  }

  private async Task<ErrorOr<CatalogItem>> CreatedAsync(string? name, Guid? categoryId, int sortOrder, double? productionMinutes, bool isQueueIndependent, CancellationToken cancellationToken)
  {
    var refusal = ProductionMinutesRefusal(productionMinutes) ?? await NameRefusalAsync(name!, null, cancellationToken) ?? await CategoryRefusalAsync(categoryId, true, cancellationToken);

    if (refusal is { } error)
      return error;

    CatalogItem created = new()
                          {
                            Id = Guid.NewGuid(),
                            Name = name!,
                            CategoryId = categoryId!.Value,
                            SortOrder = sortOrder,
                            IsActive = true,
                            ProductionMinutes = productionMinutes,
                            IsQueueIndependent = isQueueIndependent
                          };

    await _itemRepository.AddAsync(created, cancellationToken);

    await _itemRepository.SaveChangesAsync(cancellationToken);

    return created;
  }

  private async Task<ErrorOr<CatalogItem>> UpdatedAsync(Guid itemId, string? name, Guid? categoryId, int sortOrder, double? productionMinutes, bool isQueueIndependent, CancellationToken cancellationToken)
  {
    var item = await _itemRepository.FindByIdAsync(itemId, cancellationToken);

    if (item is null)
      return Refusal.CatalogItem.ItemNotFound(itemId);

    var refusal = ProductionMinutesRefusal(productionMinutes) ?? await NameRefusalAsync(name!, itemId, cancellationToken) ?? await CategoryRefusalAsync(categoryId, item.IsActive, cancellationToken);

    if (refusal is { } error)
      return error;

    item.Name = name!;
    item.CategoryId = categoryId!.Value;
    item.SortOrder = sortOrder;
    item.ProductionMinutes = productionMinutes;
    item.IsQueueIndependent = isQueueIndependent;

    await _itemRepository.SaveChangesAsync(cancellationToken);

    return item;
  }

  private async Task<ErrorOr<CatalogItem>> SwitchedOnAsync(Guid itemId, CancellationToken cancellationToken)
  {
    var item = await _itemRepository.FindByIdAsync(itemId, cancellationToken);

    if (item is null)
      return Refusal.CatalogItem.ItemNotFound(itemId);

    if (await CategoryRefusalAsync(item.CategoryId, true, cancellationToken) is { } categoryRefusal)
      return categoryRefusal;

    item.IsActive = true;
    await _itemRepository.SaveChangesAsync(cancellationToken);

    return item;
  }

  private async Task<ErrorOr<CatalogItem>> SwitchedOffAsync(Guid itemId, CancellationToken cancellationToken)
  {
    var item = await _itemRepository.FindByIdAsync(itemId, cancellationToken);

    if (item is null)
      return Refusal.CatalogItem.ItemNotFound(itemId);

    var runningFestival = await _runningFestival.FindAsync(cancellationToken);

    if (runningFestival is not null && await _itemRepository.FindMenuRowAsync(runningFestival.Id, itemId, cancellationToken) is not null)
      return Refusal.CatalogItem.ItemIsOnTheRunningFestivalsMenu(itemId);

    item.IsActive = false;
    await _itemRepository.SaveChangesAsync(cancellationToken);

    return item;
  }

  private async Task<Error?> NameRefusalAsync(string name, Guid? itemKeepingItsOwnName, CancellationToken cancellationToken)
  {
    if (!await _itemRepository.IsNameTakenAsync(name, itemKeepingItsOwnName, cancellationToken))
      return null;

    return Refusal.CatalogItem.NameTaken(name);
  }

  private async Task<Error?> CategoryRefusalAsync(Guid? categoryId, bool theArticleIsSwitchedOn, CancellationToken cancellationToken)
  {
    CatalogCategory? category = null;

    if (categoryId is not null)
      category = await _categoryRepository.FindByIdAsync(categoryId.Value, cancellationToken);

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
