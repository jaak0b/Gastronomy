using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class CatalogItemAdministrationService
{
  private const double ShortestProductionMinutes = 0;
  private const double LongestProductionMinutes = 600;

  private readonly IAfterCommitActions _afterCommitActions;
  private readonly ICatalogChangeAnnouncer _announcer;
  private readonly ICatalogCategoryRepository _categoryRepository;
  private readonly IFestivalRepository _festivalRepository;
  private readonly ICatalogItemRepository _itemRepository;
  private readonly RunningFestivalLookup _runningFestival;

  public CatalogItemAdministrationService(ICatalogItemRepository itemRepository,
                                          ICatalogCategoryRepository categoryRepository,
                                          IFestivalRepository festivalRepository,
                                          ICatalogChangeAnnouncer announcer,
                                          IAfterCommitActions afterCommitActions,
                                          RunningFestivalLookup runningFestival)
  {
    _itemRepository = itemRepository;
    _categoryRepository = categoryRepository;
    _announcer = announcer;
    _afterCommitActions = afterCommitActions;
    _festivalRepository = festivalRepository;
    _runningFestival = runningFestival;
  }

  public async Task<ErrorOr<IReadOnlyList<CatalogItem>>> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    if (festivalId is { } askedFestivalId && !await _festivalRepository.ExistsAsync(askedFestivalId, cancellationToken))
      return Refusal.CatalogItem.FestivalNotFound(askedFestivalId);

    IReadOnlyList<CatalogItem> items = await _itemRepository.FindAllOrderedAsync(festivalId, cancellationToken);

    return items.ToErrorOr();
  }

  public async Task<ErrorOr<CatalogItem>> CreateAsync(string? name, Guid? categoryId, int sortOrder, double? productionMinutes, bool isQueueIndependent, CancellationToken cancellationToken)
  {
    Error? refusal = ProductionMinutesRefusal(productionMinutes) ?? await NameRefusalAsync(name!, null, cancellationToken) ?? await CategoryRefusalAsync(categoryId, true, cancellationToken);

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

    await _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceCatalogChangedAsync, cancellationToken);

    return created;
  }

  public async Task<ErrorOr<CatalogItem>> UpdateAsync(Guid itemId, string? name, Guid? categoryId, int sortOrder, double? productionMinutes, bool isQueueIndependent, CancellationToken cancellationToken)
  {
    var item = await _itemRepository.FindByIdAsync(itemId, cancellationToken);

    if (item is null)
      return Refusal.CatalogItem.ItemNotFound(itemId);

    Error? refusal = ProductionMinutesRefusal(productionMinutes) ?? await NameRefusalAsync(name!, itemId, cancellationToken) ?? await CategoryRefusalAsync(categoryId, item.IsActive, cancellationToken);

    if (refusal is { } error)
      return error;

    item.Name = name!;
    item.CategoryId = categoryId!.Value;
    item.SortOrder = sortOrder;
    item.ProductionMinutes = productionMinutes;
    item.IsQueueIndependent = isQueueIndependent;

    await _itemRepository.SaveChangesAsync(cancellationToken);

    await _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceCatalogChangedAsync, cancellationToken);

    return item;
  }

  public async Task<ErrorOr<CatalogItem>> ActivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    var item = await _itemRepository.FindByIdAsync(itemId, cancellationToken);

    if (item is null)
      return Refusal.CatalogItem.ItemNotFound(itemId);

    if (await CategoryRefusalAsync(item.CategoryId, true, cancellationToken) is { } categoryRefusal)
      return categoryRefusal;

    item.IsActive = true;
    await _itemRepository.SaveChangesAsync(cancellationToken);

    await _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceCatalogChangedAsync, cancellationToken);

    return item;
  }

  public async Task<ErrorOr<CatalogItem>> DeactivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    var item = await _itemRepository.FindByIdAsync(itemId, cancellationToken);

    if (item is null)
      return Refusal.CatalogItem.ItemNotFound(itemId);

    var runningFestival = await _runningFestival.FindAsync(cancellationToken);

    if (runningFestival is not null && await _itemRepository.FindMenuRowAsync(runningFestival.Id, itemId, cancellationToken) is not null)
      return Refusal.CatalogItem.ItemIsOnTheRunningFestivalsMenu(itemId);

    item.IsActive = false;
    await _itemRepository.SaveChangesAsync(cancellationToken);

    await _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceCatalogChangedAsync, cancellationToken);

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
