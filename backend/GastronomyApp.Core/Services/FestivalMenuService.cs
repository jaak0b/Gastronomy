using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class FestivalMenuService
{
  private readonly IFestivalRepository _festivalRepository;
  private readonly ItemOrderability _orderability;
  private readonly IFestivalMenuRepository _repository;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly ITransactionRunner _transactionRunner;

  public FestivalMenuService(IFestivalMenuRepository repository, IFestivalRepository festivalRepository, ItemOrderability orderability, RunningFestivalLookup runningFestival, ITransactionRunner transactionRunner)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
    _orderability = orderability;
    _runningFestival = runningFestival;
    _transactionRunner = transactionRunner;
  }

  public Task<ErrorOr<FestivalCatalogItem>> PutOnTheMenuAsync(Guid festivalId, Guid catalogItemId, int priceCents, IReadOnlyList<Guid>? stationIds, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => PutOnAsync(festivalId, catalogItemId, priceCents, stationIds, transactionCancellationToken), cancellationToken);
  }

  public Task<ErrorOr<FestivalCatalogItem>> TakeOffTheMenuAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => TakenOffAsync(festivalId, catalogItemId, transactionCancellationToken), cancellationToken);
  }

  public Task<ErrorOr<FestivalCatalogItem>> SetAvailabilityAsync(Guid festivalId, Guid catalogItemId, bool isAvailable, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => AvailabilitySetAsync(festivalId, catalogItemId, isAvailable, transactionCancellationToken), cancellationToken);
  }

  private async Task<ErrorOr<FestivalCatalogItem>> PutOnAsync(Guid festivalId, Guid catalogItemId, int priceCents, IReadOnlyList<Guid>? stationIdsRequested, CancellationToken cancellationToken)
  {
    if (!await _festivalRepository.ExistsAsync(festivalId, cancellationToken))
      return Refusal.FestivalMenu.FestivalNotFound(festivalId);

    if (!await _repository.CatalogItemExistsAsync(catalogItemId, cancellationToken))
      return Refusal.FestivalMenu.CatalogItemNotFound(catalogItemId);

    List<Guid> stationIds = (stationIdsRequested ?? []).ToList();

    IReadOnlyList<Guid> stationIdsAtTheFestival = await _repository.FindStationIdsAtFestivalAsync(festivalId, cancellationToken);

    List<Guid> strangers = stationIds.Where(stationId => !stationIdsAtTheFestival.Contains(stationId)).ToList();

    if (strangers.Count > 0)
      return Refusal.FestivalMenu.StationsDoNotBelongToTheFestival(festivalId, catalogItemId, strangers);

    if (!await _orderability.AnyOfTheseStationsPreparesAtAsync(festivalId, stationIds, cancellationToken))
      return Refusal.FestivalMenu.NoStationPreparesTheItem(catalogItemId);

    var menuRow = await _repository.FindMenuRowAsync(festivalId, catalogItemId, cancellationToken);

    if (menuRow is null)
    {
      menuRow = new()
                {
                  Id = Guid.NewGuid(),
                  FestivalId = festivalId,
                  CatalogItemId = catalogItemId,
                  PriceCents = priceCents,
                  IsAvailable = true
                };

      await _repository.AddMenuRowAsync(menuRow, cancellationToken);
    }
    else
      menuRow.PriceCents = priceCents;

    IReadOnlyList<ItemStationAssignment> existing = await _repository.FindAssignmentsAsync(festivalId, catalogItemId, cancellationToken);

    _repository.RemoveAssignments(existing);

    foreach (var stationId in stationIds.Distinct())
      await _repository.AddAssignmentAsync(new()
                                           {
                                             Id = Guid.NewGuid(),
                                             FestivalId = festivalId,
                                             CatalogItemId = catalogItemId,
                                             StationId = stationId
                                           },
                                           cancellationToken);

    await _repository.SaveChangesAsync(cancellationToken);

    return menuRow;
  }

  private async Task<ErrorOr<FestivalCatalogItem>> TakenOffAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken)
  {
    var menuRow = await _repository.FindMenuRowAsync(festivalId, catalogItemId, cancellationToken);

    if (menuRow is null)
      return Refusal.FestivalMenu.MenuRowNotFound(festivalId, catalogItemId);

    var festival = await _festivalRepository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Refusal.FestivalMenu.FestivalNotFound(festivalId);

    if (_runningFestival.IsRunning(festival))
      return Refusal.FestivalMenu.FestivalIsRunning(festivalId);

    IReadOnlyList<ItemStationAssignment> assignments = await _repository.FindAssignmentsAsync(festivalId, catalogItemId, cancellationToken);

    _repository.RemoveAssignments(assignments);
    _repository.RemoveMenuRow(menuRow);

    await _repository.SaveChangesAsync(cancellationToken);

    return menuRow;
  }

  private async Task<ErrorOr<FestivalCatalogItem>> AvailabilitySetAsync(Guid festivalId, Guid catalogItemId, bool isAvailable, CancellationToken cancellationToken)
  {
    var menuRow = await _repository.FindMenuRowAsync(festivalId, catalogItemId, cancellationToken);

    if (menuRow is null)
      return Refusal.FestivalMenu.MenuRowNotFound(festivalId, catalogItemId);

    if (menuRow.IsAvailable == isAvailable)
      return menuRow;

    menuRow.IsAvailable = isAvailable;
    await _repository.SaveChangesAsync(cancellationToken);

    return menuRow;
  }
}
