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
  private readonly IClock _clock;
  private readonly IFestivalRepository _festivalRepository;
  private readonly ICatalogItemRepository _itemRepository;
  private readonly ITransactionRunner _transactionRunner;

  public CatalogItemAdministrationService(ICatalogItemRepository itemRepository,
                                          ICatalogCategoryRepository categoryRepository,
                                          IFestivalRepository festivalRepository,
                                          ITransactionRunner transactionRunner,
                                          IClock clock)
  {
    _itemRepository = itemRepository;
    _categoryRepository = categoryRepository;
    _festivalRepository = festivalRepository;
    _transactionRunner = transactionRunner;
    _clock = clock;
  }

  public async Task<Result<IReadOnlyList<AdministeredCatalogItem>, CatalogItemAdministrationFailure>> ListAsync(
    Guid? festivalId,
    CancellationToken cancellationToken)
  {
    if (festivalId is { } askedFestivalId
        && !await _festivalRepository.ExistsAsync(askedFestivalId, cancellationToken))
    {
      return Failed<IReadOnlyList<AdministeredCatalogItem>>(CatalogItemAdministrationFailureReason.FestivalNotFound);
    }

    IReadOnlyList<CatalogItem> items = await _itemRepository.FindAllOrderedAsync(cancellationToken);

    IReadOnlyList<FestivalCatalogItem> menuRows = festivalId is null
                                                    ? []
                                                    : await _itemRepository.FindMenuRowsAtFestivalAsync(festivalId.Value,
                                                                                                        cancellationToken);

    IReadOnlyList<ItemStationAssignment> assignments =
      festivalId is null
        ? []
        : await _itemRepository.FindAssignmentsAtFestivalAsync(festivalId.Value, cancellationToken);

    Dictionary<Guid, FestivalCatalogItem> menuRowsByItemId = menuRows.ToDictionary(menuRow => menuRow.CatalogItemId);

    IReadOnlyList<AdministeredCatalogItem> administered =
    [
      .. items.Select(item => new AdministeredCatalogItem(item.Id,
                                                          item.Name,
                                                          item.CategoryId,
                                                          item.SortOrder,
                                                          item.IsActive,
                                                          item.ProductionMinutes,
                                                          item.IsQueueIndependent,
                                                          BuildItemAtFestival(item.Id, menuRowsByItemId, assignments)))
    ];

    return Result<IReadOnlyList<AdministeredCatalogItem>, CatalogItemAdministrationFailure>.Success(administered);
  }

  public Task<Result<Guid, CatalogItemAdministrationFailure>> CreateAsync(SaveCatalogItemRequest request,
                                                                          CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return RunAsync(transactionCancellationToken => CreatedAsync(request, transactionCancellationToken),
                    cancellationToken);
  }

  public Task<Result<Guid, CatalogItemAdministrationFailure>> UpdateAsync(Guid itemId,
                                                                          SaveCatalogItemRequest request,
                                                                          CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return RunAsync(transactionCancellationToken => UpdatedAsync(itemId, request, transactionCancellationToken),
                    cancellationToken);
  }

  public Task<Result<Guid, CatalogItemAdministrationFailure>> ActivateAsync(Guid itemId,
                                                                            CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOnAsync(itemId, transactionCancellationToken),
                    cancellationToken);
  }

  public Task<Result<Guid, CatalogItemAdministrationFailure>> DeactivateAsync(Guid itemId,
                                                                              CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOffAsync(itemId, transactionCancellationToken),
                    cancellationToken);
  }

  private async Task<Result<Guid, CatalogItemAdministrationFailure>> CreatedAsync(SaveCatalogItemRequest request,
                                                                                  CancellationToken cancellationToken)
  {
    CatalogItemAdministrationFailure? refusal =
      Validate(request)
      ?? (await _itemRepository.IsNameTakenAsync(request.Name!, null, cancellationToken)
            ? new CatalogItemAdministrationFailure { Reason = CatalogItemAdministrationFailureReason.NameTaken }
            : null)
      ?? await CategoryRefusalAsync(request.CategoryId, true, cancellationToken);

    if (refusal is not null)
    {
      return Result<Guid, CatalogItemAdministrationFailure>.Failed(refusal);
    }

    var itemId = Guid.NewGuid();

    await _itemRepository.AddAsync(new()
                                   {
                                     Id = itemId,
                                     Name = request.Name!,
                                     CategoryId = request.CategoryId!.Value,
                                     SortOrder = request.SortOrder,
                                     IsActive = true,
                                     ProductionMinutes = request.ProductionMinutes,
                                     IsQueueIndependent = request.IsQueueIndependent
                                   },
                                   cancellationToken);

    await _itemRepository.SaveChangesAsync(cancellationToken);

    return Result<Guid, CatalogItemAdministrationFailure>.Success(itemId);
  }

  private async Task<Result<Guid, CatalogItemAdministrationFailure>> UpdatedAsync(Guid itemId,
                                                                                  SaveCatalogItemRequest request,
                                                                                  CancellationToken cancellationToken)
  {
    CatalogItem? item = await _itemRepository.FindByIdAsync(itemId, cancellationToken);

    if (item is null)
    {
      return Failed<Guid>(CatalogItemAdministrationFailureReason.ItemNotFound);
    }

    CatalogItemAdministrationFailure? refusal =
      Validate(request)
      ?? (await _itemRepository.IsNameTakenAsync(request.Name!, itemId, cancellationToken)
            ? new CatalogItemAdministrationFailure { Reason = CatalogItemAdministrationFailureReason.NameTaken }
            : null)
      ?? await CategoryRefusalAsync(request.CategoryId, item.IsActive, cancellationToken);

    if (refusal is not null)
    {
      return Result<Guid, CatalogItemAdministrationFailure>.Failed(refusal);
    }

    item.Name = request.Name!;
    item.CategoryId = request.CategoryId!.Value;
    item.SortOrder = request.SortOrder;
    item.ProductionMinutes = request.ProductionMinutes;
    item.IsQueueIndependent = request.IsQueueIndependent;

    await _itemRepository.SaveChangesAsync(cancellationToken);

    return Result<Guid, CatalogItemAdministrationFailure>.Success(itemId);
  }

  private async Task<Result<Guid, CatalogItemAdministrationFailure>> SwitchedOnAsync(Guid itemId,
                                                                                     CancellationToken cancellationToken)
  {
    CatalogItem? item = await _itemRepository.FindByIdAsync(itemId, cancellationToken);

    if (item is null)
    {
      return Failed<Guid>(CatalogItemAdministrationFailureReason.ItemNotFound);
    }

    CatalogItemAdministrationFailure? categoryRefusal =
      await CategoryRefusalAsync(item.CategoryId, true, cancellationToken);

    if (categoryRefusal is not null)
    {
      return Result<Guid, CatalogItemAdministrationFailure>.Failed(categoryRefusal);
    }

    item.IsActive = true;
    await _itemRepository.SaveChangesAsync(cancellationToken);

    return Result<Guid, CatalogItemAdministrationFailure>.Success(itemId);
  }

  private async Task<Result<Guid, CatalogItemAdministrationFailure>> SwitchedOffAsync(Guid itemId,
                                                                                      CancellationToken cancellationToken)
  {
    CatalogItem? item = await _itemRepository.FindByIdAsync(itemId, cancellationToken);

    if (item is null)
    {
      return Failed<Guid>(CatalogItemAdministrationFailureReason.ItemNotFound);
    }

    Festival? runningFestival = await _festivalRepository.FindRunningAsync(_clock.UtcNow, cancellationToken);

    if (runningFestival is not null
        && await _itemRepository.FindMenuRowAsync(runningFestival.Id, itemId, cancellationToken) is not null)
    {
      return Failed<Guid>(CatalogItemAdministrationFailureReason.ItemIsOnTheRunningFestivalsMenu);
    }

    item.IsActive = false;
    await _itemRepository.SaveChangesAsync(cancellationToken);

    return Result<Guid, CatalogItemAdministrationFailure>.Success(itemId);
  }

  private async Task<CatalogItemAdministrationFailure?> CategoryRefusalAsync(Guid? categoryId,
                                                                             bool theArticleIsSwitchedOn,
                                                                             CancellationToken cancellationToken)
  {
    CatalogCategory? category = categoryId is null
                                  ? null
                                  : await _categoryRepository.FindByIdAsync(categoryId.Value, cancellationToken);

    if (category is null)
    {
      return new() { Reason = CatalogItemAdministrationFailureReason.CategoryUnknown };
    }

    return category.IsActive || !theArticleIsSwitchedOn
             ? null
             : new CatalogItemAdministrationFailure
               {
                 Reason = CatalogItemAdministrationFailureReason.CategoryIsSwitchedOff
               };
  }

  private CatalogItemAdministrationFailure? Validate(SaveCatalogItemRequest request)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return new() { Reason = CatalogItemAdministrationFailureReason.NameMissing };
    }

    if (request.ProductionMinutes is { } minutes
        && (minutes is < ShortestProductionMinutes or > LongestProductionMinutes
            || Math.Round(minutes, 1) != minutes))
    {
      return new()
             {
               Reason = CatalogItemAdministrationFailureReason.ProductionMinutesOutOfRange,
               OffendingProductionMinutes = minutes
             };
    }

    return null;
  }

  private CatalogItemAtFestival? BuildItemAtFestival(Guid itemId,
                                                     IReadOnlyDictionary<Guid, FestivalCatalogItem> menuRowsByItemId,
                                                     IReadOnlyCollection<ItemStationAssignment> assignments)
  {
    if (!menuRowsByItemId.TryGetValue(itemId, out var menuRow))
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

  private Result<TValue, CatalogItemAdministrationFailure> Failed<TValue>(CatalogItemAdministrationFailureReason reason)
  {
    return Result<TValue, CatalogItemAdministrationFailure>.Failed(new() { Reason = reason });
  }

  private async Task<Result<Guid, CatalogItemAdministrationFailure>> RunAsync(
    Func<CancellationToken, Task<Result<Guid, CatalogItemAdministrationFailure>>> write,
    CancellationToken cancellationToken)
  {
    return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                             {
                                               Result<Guid, CatalogItemAdministrationFailure> written =
                                                 await write(transactionCancellationToken);

                                               return new TransactionOutcome<Result<Guid, CatalogItemAdministrationFailure>>
                                                      {
                                                        Value = written,
                                                        ShouldCommit = written.IsSuccess
                                                      };
                                             },
                                             cancellationToken);
  }
}
