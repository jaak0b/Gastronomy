using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;

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

  public async Task<Result<IReadOnlyList<AdministeredCatalogItem>, CatalogItemAdministrationFailure>> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    if (festivalId is { } askedFestivalId && !await _festivalRepository.ExistsAsync(askedFestivalId, cancellationToken))
      return Failed<IReadOnlyList<AdministeredCatalogItem>>(CatalogItemAdministrationFailureReason.FestivalNotFound);

    IReadOnlyList<CatalogItem> items = await _itemRepository.FindAllOrderedAsync(cancellationToken);

    IReadOnlyList<FestivalCatalogItem> menuRows = [];
    IReadOnlyList<ItemStationAssignment> assignments = [];

    if (festivalId is not null)
    {
      menuRows = await _itemRepository.FindMenuRowsAtFestivalAsync(festivalId.Value, cancellationToken);
      assignments = await _itemRepository.FindAssignmentsAtFestivalAsync(festivalId.Value, cancellationToken);
    }

    Dictionary<Guid, FestivalCatalogItem> menuRowsByItemId = menuRows.ToDictionary(menuRow => menuRow.CatalogItemId);

    IReadOnlyList<AdministeredCatalogItem> administered = items.Select(item => new AdministeredCatalogItem(item.Id, item.Name, item.CategoryId, item.SortOrder, item.IsActive, item.ProductionMinutes, item.IsQueueIndependent, BuildItemAtFestival(item.Id, menuRowsByItemId, assignments))).ToList();

    return Result<IReadOnlyList<AdministeredCatalogItem>, CatalogItemAdministrationFailure>.Success(administered);
  }

  public Task<Result<AdministeredCatalogItem, CatalogItemAdministrationFailure>> CreateAsync(string? name, Guid? categoryId, int sortOrder, double? productionMinutes, bool isQueueIndependent, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => CreatedAsync(name, categoryId, sortOrder, productionMinutes, isQueueIndependent, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<Guid, CatalogItemAdministrationFailure>> UpdateAsync(Guid itemId, string? name, Guid? categoryId, int sortOrder, double? productionMinutes, bool isQueueIndependent, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => UpdatedAsync(itemId, name, categoryId, sortOrder, productionMinutes, isQueueIndependent, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<Guid, CatalogItemAdministrationFailure>> ActivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOnAsync(itemId, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<Guid, CatalogItemAdministrationFailure>> DeactivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOffAsync(itemId, transactionCancellationToken), cancellationToken);
  }

  private async Task<Result<AdministeredCatalogItem, CatalogItemAdministrationFailure>> CreatedAsync(string? name, Guid? categoryId, int sortOrder, double? productionMinutes, bool isQueueIndependent, CancellationToken cancellationToken)
  {
    var refusal = Validate(name, productionMinutes) ?? await NameRefusalAsync(name!, null, cancellationToken) ?? await CategoryRefusalAsync(categoryId, true, cancellationToken);

    if (refusal is not null)
      return Result<AdministeredCatalogItem, CatalogItemAdministrationFailure>.Failed(refusal);

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

    return Result<AdministeredCatalogItem, CatalogItemAdministrationFailure>.Success(new(created.Id, created.Name, created.CategoryId, created.SortOrder, created.IsActive, created.ProductionMinutes, created.IsQueueIndependent, null));
  }

  private async Task<Result<Guid, CatalogItemAdministrationFailure>> UpdatedAsync(Guid itemId, string? name, Guid? categoryId, int sortOrder, double? productionMinutes, bool isQueueIndependent, CancellationToken cancellationToken)
  {
    var item = await _itemRepository.FindByIdAsync(itemId, cancellationToken);

    if (item is null)
      return Failed<Guid>(CatalogItemAdministrationFailureReason.ItemNotFound);

    var refusal = Validate(name, productionMinutes) ?? await NameRefusalAsync(name!, itemId, cancellationToken) ?? await CategoryRefusalAsync(categoryId, item.IsActive, cancellationToken);

    if (refusal is not null)
      return Result<Guid, CatalogItemAdministrationFailure>.Failed(refusal);

    item.Name = name!;
    item.CategoryId = categoryId!.Value;
    item.SortOrder = sortOrder;
    item.ProductionMinutes = productionMinutes;
    item.IsQueueIndependent = isQueueIndependent;

    await _itemRepository.SaveChangesAsync(cancellationToken);

    return Result<Guid, CatalogItemAdministrationFailure>.Success(itemId);
  }

  private async Task<Result<Guid, CatalogItemAdministrationFailure>> SwitchedOnAsync(Guid itemId, CancellationToken cancellationToken)
  {
    var item = await _itemRepository.FindByIdAsync(itemId, cancellationToken);

    if (item is null)
      return Failed<Guid>(CatalogItemAdministrationFailureReason.ItemNotFound);

    var categoryRefusal = await CategoryRefusalAsync(item.CategoryId, true, cancellationToken);

    if (categoryRefusal is not null)
      return Result<Guid, CatalogItemAdministrationFailure>.Failed(categoryRefusal);

    item.IsActive = true;
    await _itemRepository.SaveChangesAsync(cancellationToken);

    return Result<Guid, CatalogItemAdministrationFailure>.Success(itemId);
  }

  private async Task<Result<Guid, CatalogItemAdministrationFailure>> SwitchedOffAsync(Guid itemId, CancellationToken cancellationToken)
  {
    var item = await _itemRepository.FindByIdAsync(itemId, cancellationToken);

    if (item is null)
      return Failed<Guid>(CatalogItemAdministrationFailureReason.ItemNotFound);

    var runningFestival = await _runningFestival.FindAsync(cancellationToken);

    if (runningFestival is not null && await _itemRepository.FindMenuRowAsync(runningFestival.Id, itemId, cancellationToken) is not null)
      return Failed<Guid>(CatalogItemAdministrationFailureReason.ItemIsOnTheRunningFestivalsMenu);

    item.IsActive = false;
    await _itemRepository.SaveChangesAsync(cancellationToken);

    return Result<Guid, CatalogItemAdministrationFailure>.Success(itemId);
  }

  private async Task<CatalogItemAdministrationFailure?> NameRefusalAsync(string name, Guid? itemKeepingItsOwnName, CancellationToken cancellationToken)
  {
    if (!await _itemRepository.IsNameTakenAsync(name, itemKeepingItsOwnName, cancellationToken))
      return null;

    return new() { Reason = CatalogItemAdministrationFailureReason.NameTaken };
  }

  private async Task<CatalogItemAdministrationFailure?> CategoryRefusalAsync(Guid? categoryId, bool theArticleIsSwitchedOn, CancellationToken cancellationToken)
  {
    CatalogCategory? category = null;

    if (categoryId is not null)
      category = await _categoryRepository.FindByIdAsync(categoryId.Value, cancellationToken);

    if (category is null)
      return new() { Reason = CatalogItemAdministrationFailureReason.CategoryUnknown };

    if (category.IsActive || !theArticleIsSwitchedOn)
      return null;

    return new() { Reason = CatalogItemAdministrationFailureReason.CategoryIsSwitchedOff };
  }

  private CatalogItemAdministrationFailure? Validate(string? name, double? productionMinutes)
  {
    if (string.IsNullOrWhiteSpace(name))
      return new() { Reason = CatalogItemAdministrationFailureReason.NameMissing };

    if (productionMinutes is { } minutes && (minutes is < ShortestProductionMinutes or > LongestProductionMinutes || Math.Round(minutes, 1) != minutes))
    {
      return new()
             {
               Reason = CatalogItemAdministrationFailureReason.ProductionMinutesOutOfRange,
               OffendingProductionMinutes = minutes
             };
    }

    return null;
  }

  private CatalogItemAtFestival? BuildItemAtFestival(Guid itemId, IReadOnlyDictionary<Guid, FestivalCatalogItem> menuRowsByItemId, IReadOnlyCollection<ItemStationAssignment> assignments)
  {
    if (!menuRowsByItemId.TryGetValue(itemId, out var menuRow))
      return null;

    return new(menuRow.PriceCents, menuRow.IsAvailable, assignments.Where(assignment => assignment.CatalogItemId == itemId).Select(assignment => assignment.StationId).ToList());
  }

  private Result<TValue, CatalogItemAdministrationFailure> Failed<TValue>(CatalogItemAdministrationFailureReason reason)
  {
    return Result<TValue, CatalogItemAdministrationFailure>.Failed(new() { Reason = reason });
  }

  private async Task<Result<TValue, CatalogItemAdministrationFailure>> RunAsync<TValue>(Func<CancellationToken, Task<Result<TValue, CatalogItemAdministrationFailure>>> write, CancellationToken cancellationToken)
  {
    return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                             {
                                               Result<TValue, CatalogItemAdministrationFailure> written = await write(transactionCancellationToken);

                                               return new TransactionOutcome<Result<TValue, CatalogItemAdministrationFailure>>
                                                      {
                                                        Value = written,
                                                        ShouldCommit = written.IsSuccess
                                                      };
                                             },
                                             cancellationToken);
  }
}
