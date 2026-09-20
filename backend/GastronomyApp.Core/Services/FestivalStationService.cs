using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class FestivalStationService
{
  private readonly IClock _clock;
  private readonly IFestivalRepository _festivalRepository;
  private readonly INumberAllocator _numberAllocator;
  private readonly ItemOrderability _orderability;
  private readonly IFestivalStationRepository _repository;
  private readonly FestivalSchedule _schedule;
  private readonly IStationRepository _stationRepository;
  private readonly ITransactionRunner _transactionRunner;

  public FestivalStationService(IFestivalStationRepository repository,
                                IFestivalRepository festivalRepository,
                                IStationRepository stationRepository,
                                ItemOrderability orderability,
                                INumberAllocator numberAllocator,
                                FestivalSchedule schedule,
                                ITransactionRunner transactionRunner,
                                IClock clock)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
    _stationRepository = stationRepository;
    _orderability = orderability;
    _numberAllocator = numberAllocator;
    _schedule = schedule;
    _transactionRunner = transactionRunner;
    _clock = clock;
  }

  public Task<Result<SavedFestivalStation, FestivalStationFailure>> AddAsync(Guid festivalId,
                                                                             Guid stationId,
                                                                             CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => AddedAsync(festivalId, stationId, transactionCancellationToken),
                    cancellationToken);
  }

  public Task<Result<SavedFestivalStation, FestivalStationFailure>> RemoveAsync(Guid festivalId,
                                                                                Guid stationId,
                                                                                CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => RemovedAsync(festivalId, stationId, transactionCancellationToken),
                    cancellationToken);
  }

  private async Task<Result<SavedFestivalStation, FestivalStationFailure>> AddedAsync(
    Guid festivalId,
    Guid stationId,
    CancellationToken cancellationToken)
  {
    if (!await _festivalRepository.ExistsAsync(festivalId, cancellationToken))
    {
      return Failed(FestivalStationFailureReason.FestivalNotFound);
    }

    if (!await _stationRepository.ExistsAsync(stationId, cancellationToken))
    {
      return Failed(FestivalStationFailureReason.StationNotFound);
    }

    if (await _repository.FindLinkAsync(festivalId, stationId, cancellationToken) is not null)
    {
      return Saved(stationId, false);
    }

    var nextStationOrderNumber =
      await _numberAllocator.FindNextStationOrderNumberAsync(festivalId, stationId, cancellationToken);

    await _repository.AddLinkAsync(new()
                                   {
                                     Id = Guid.NewGuid(),
                                     FestivalId = festivalId,
                                     StationId = stationId,
                                     NextStationOrderNumber = nextStationOrderNumber
                                   },
                                   cancellationToken);

    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(stationId, true);
  }

  private async Task<Result<SavedFestivalStation, FestivalStationFailure>> RemovedAsync(
    Guid festivalId,
    Guid stationId,
    CancellationToken cancellationToken)
  {
    FestivalStation? link = await _repository.FindLinkAsync(festivalId, stationId, cancellationToken);

    if (link is null)
    {
      return Failed(FestivalStationFailureReason.StationLinkNotFound);
    }

    Festival? festival = await _festivalRepository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
    {
      return Failed(FestivalStationFailureReason.FestivalNotFound);
    }

    if (_schedule.IsRunning(festival, _clock.UtcNow)
        && await _repository.CountUnfulfilledItemsAsync(festivalId, stationId, cancellationToken) > 0)
    {
      return Failed(FestivalStationFailureReason.StationHasUnfulfilledItems);
    }

    IReadOnlyList<Guid> strandedItemIds =
      await _orderability.FindItemsStrandedByRemovingStationsAsync(festivalId, [stationId], cancellationToken);

    if (strandedItemIds.Count > 0)
    {
      return Result<SavedFestivalStation, FestivalStationFailure>
        .Failed(new()
                {
                  Reason = FestivalStationFailureReason.ItemsWouldHaveNoStation,
                  StrandedItemCount = strandedItemIds.Count
                });
    }

    IReadOnlyList<ItemStationAssignment> assignmentsHere =
      await _repository.FindAssignmentsAtStationAsync(festivalId, stationId, cancellationToken);

    _repository.RemoveAssignments(assignmentsHere);
    _repository.RemoveLink(link);

    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(stationId, true);
  }

  private Result<SavedFestivalStation, FestivalStationFailure> Saved(Guid stationId, bool somethingChanged)
  {
    return Result<SavedFestivalStation, FestivalStationFailure>.Success(new(stationId, somethingChanged));
  }

  private Result<SavedFestivalStation, FestivalStationFailure> Failed(FestivalStationFailureReason reason)
  {
    return Result<SavedFestivalStation, FestivalStationFailure>.Failed(new() { Reason = reason });
  }

  private async Task<Result<SavedFestivalStation, FestivalStationFailure>> RunAsync(
    Func<CancellationToken, Task<Result<SavedFestivalStation, FestivalStationFailure>>> write,
    CancellationToken cancellationToken)
  {
    return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                             {
                                               Result<SavedFestivalStation, FestivalStationFailure> written =
                                                 await write(transactionCancellationToken);

                                               return new TransactionOutcome<Result<SavedFestivalStation, FestivalStationFailure>>
                                                      {
                                                        Value = written,
                                                        ShouldCommit = written.IsSuccess && written.Value.SomethingChanged
                                                      };
                                             },
                                             cancellationToken);
  }
}
