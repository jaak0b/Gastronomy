using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class FestivalMenuService
{
  private const int HighestPriceCents = 99999;
  private const int LowestPriceCents = 0;

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

  public Task<Result<SavedFestivalMenuItem, FestivalMenuFailure>> PutOnTheMenuAsync(Guid festivalId, Guid catalogItemId, int priceCents, IReadOnlyList<Guid>? stationIds, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => PutOnAsync(festivalId, catalogItemId, priceCents, stationIds, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<SavedFestivalMenuItem, FestivalMenuFailure>> TakeOffTheMenuAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => TakenOffAsync(festivalId, catalogItemId, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<SavedFestivalMenuItem, FestivalMenuFailure>> SetAvailabilityAsync(Guid festivalId, Guid catalogItemId, bool isAvailable, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => AvailabilitySetAsync(festivalId, catalogItemId, isAvailable, transactionCancellationToken), cancellationToken);
  }

  private async Task<Result<SavedFestivalMenuItem, FestivalMenuFailure>> PutOnAsync(Guid festivalId, Guid catalogItemId, int priceCents, IReadOnlyList<Guid>? stationIdsRequested, CancellationToken cancellationToken)
  {
    if (!await _festivalRepository.ExistsAsync(festivalId, cancellationToken))
      return Failed(FestivalMenuFailureReason.FestivalNotFound);

    if (!await _repository.CatalogItemExistsAsync(catalogItemId, cancellationToken))
      return Failed(FestivalMenuFailureReason.CatalogItemNotFound);

    if (priceCents is < LowestPriceCents or > HighestPriceCents)
      return Failed(FestivalMenuFailureReason.PriceOutOfRange);

    List<Guid> stationIds = (stationIdsRequested ?? []).ToList();

    IReadOnlyList<Guid> stationIdsAtTheFestival = await _repository.FindStationIdsAtFestivalAsync(festivalId, cancellationToken);

    List<Guid> strangers = stationIds.Where(stationId => !stationIdsAtTheFestival.Contains(stationId)).ToList();

    if (strangers.Count > 0)
    {
      return Result<SavedFestivalMenuItem, FestivalMenuFailure>.Failed(new()
                                                                       {
                                                                         Reason = FestivalMenuFailureReason.StationsDoNotBelongToTheFestival,
                                                                         StationIdsOutsideTheFestival = strangers
                                                                       });
    }

    if (!await _orderability.AnyOfTheseStationsPreparesAtAsync(festivalId, stationIds, cancellationToken))
      return Failed(FestivalMenuFailureReason.NoStationPreparesTheItem);

    var menuRow = await _repository.FindMenuRowAsync(festivalId, catalogItemId, cancellationToken);

    if (menuRow is null)
    {
      await _repository.AddMenuRowAsync(new()
                                        {
                                          Id = Guid.NewGuid(),
                                          FestivalId = festivalId,
                                          CatalogItemId = catalogItemId,
                                          PriceCents = priceCents,
                                          IsAvailable = true
                                        },
                                        cancellationToken);
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

    return Saved(catalogItemId, true);
  }

  private async Task<Result<SavedFestivalMenuItem, FestivalMenuFailure>> TakenOffAsync(Guid festivalId, Guid catalogItemId, CancellationToken cancellationToken)
  {
    var menuRow = await _repository.FindMenuRowAsync(festivalId, catalogItemId, cancellationToken);

    if (menuRow is null)
      return Failed(FestivalMenuFailureReason.MenuRowNotFound);

    var festival = await _festivalRepository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Failed(FestivalMenuFailureReason.FestivalNotFound);

    if (_runningFestival.IsRunning(festival))
      return Failed(FestivalMenuFailureReason.FestivalIsRunning);

    IReadOnlyList<ItemStationAssignment> assignments = await _repository.FindAssignmentsAsync(festivalId, catalogItemId, cancellationToken);

    _repository.RemoveAssignments(assignments);
    _repository.RemoveMenuRow(menuRow);

    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(catalogItemId, true);
  }

  private async Task<Result<SavedFestivalMenuItem, FestivalMenuFailure>> AvailabilitySetAsync(Guid festivalId, Guid catalogItemId, bool isAvailable, CancellationToken cancellationToken)
  {
    var menuRow = await _repository.FindMenuRowAsync(festivalId, catalogItemId, cancellationToken);

    if (menuRow is null)
      return Failed(FestivalMenuFailureReason.MenuRowNotFound);

    if (menuRow.IsAvailable == isAvailable)
      return Saved(catalogItemId, false);

    menuRow.IsAvailable = isAvailable;
    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(catalogItemId, true);
  }

  private Result<SavedFestivalMenuItem, FestivalMenuFailure> Saved(Guid catalogItemId, bool somethingChanged)
  {
    return Result<SavedFestivalMenuItem, FestivalMenuFailure>.Success(new(catalogItemId, somethingChanged));
  }

  private Result<SavedFestivalMenuItem, FestivalMenuFailure> Failed(FestivalMenuFailureReason reason)
  {
    return Result<SavedFestivalMenuItem, FestivalMenuFailure>.Failed(new() { Reason = reason });
  }

  private async Task<Result<SavedFestivalMenuItem, FestivalMenuFailure>> RunAsync(Func<CancellationToken, Task<Result<SavedFestivalMenuItem, FestivalMenuFailure>>> write, CancellationToken cancellationToken)
  {
    return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                             {
                                               Result<SavedFestivalMenuItem, FestivalMenuFailure> written = await write(transactionCancellationToken);

                                               return new TransactionOutcome<Result<SavedFestivalMenuItem, FestivalMenuFailure>>
                                                      {
                                                        Value = written,
                                                        ShouldCommit = written.IsSuccess && written.Value.SomethingChanged
                                                      };
                                             },
                                             cancellationToken);
  }
}
