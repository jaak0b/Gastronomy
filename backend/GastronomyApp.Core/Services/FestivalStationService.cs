using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class FestivalStationService
{
  private readonly IFestivalRepository _festivalRepository;
  private readonly INumberAllocator _numberAllocator;
  private readonly ItemOrderability _orderability;
  private readonly IFestivalStationRepository _repository;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly IStationRepository _stationRepository;
  private readonly ITransactionRunner _transactionRunner;

  public FestivalStationService(IFestivalStationRepository repository, IFestivalRepository festivalRepository, IStationRepository stationRepository, ItemOrderability orderability, INumberAllocator numberAllocator, RunningFestivalLookup runningFestival, ITransactionRunner transactionRunner)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
    _stationRepository = stationRepository;
    _orderability = orderability;
    _numberAllocator = numberAllocator;
    _runningFestival = runningFestival;
    _transactionRunner = transactionRunner;
  }

  public Task<Result<FestivalStation?, FestivalStationFailure>> AddAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => AddedAsync(festivalId, stationId, transactionCancellationToken), written => written.IsSuccess && written.Value is not null, cancellationToken);
  }

  public Task<Result<FestivalStation, FestivalStationFailure>> RemoveAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => RemovedAsync(festivalId, stationId, transactionCancellationToken), written => written.IsSuccess, cancellationToken);
  }

  private async Task<Result<FestivalStation?, FestivalStationFailure>> AddedAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    if (!await _festivalRepository.ExistsAsync(festivalId, cancellationToken))
      return Failed<FestivalStation?>(FestivalStationFailureReason.FestivalNotFound);

    if (!await _stationRepository.ExistsAsync(stationId, cancellationToken))
      return Failed<FestivalStation?>(FestivalStationFailureReason.StationNotFound);

    if (await _repository.FindLinkAsync(festivalId, stationId, cancellationToken) is not null)
      return Result<FestivalStation?, FestivalStationFailure>.Success(null);

    var nextStationOrderNumber = await _numberAllocator.FindNextStationOrderNumberAsync(festivalId, stationId, cancellationToken);

    FestivalStation link = new()
                           {
                             Id = Guid.NewGuid(),
                             FestivalId = festivalId,
                             StationId = stationId,
                             NextStationOrderNumber = nextStationOrderNumber
                           };

    await _repository.AddLinkAsync(link, cancellationToken);

    await _repository.SaveChangesAsync(cancellationToken);

    return Result<FestivalStation?, FestivalStationFailure>.Success(link);
  }

  private async Task<Result<FestivalStation, FestivalStationFailure>> RemovedAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    var link = await _repository.FindLinkAsync(festivalId, stationId, cancellationToken);

    if (link is null)
      return Failed<FestivalStation>(FestivalStationFailureReason.StationLinkNotFound);

    var festival = await _festivalRepository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Failed<FestivalStation>(FestivalStationFailureReason.FestivalNotFound);

    if (_runningFestival.IsRunning(festival) && await _repository.CountUnfulfilledItemsAsync(festivalId, stationId, cancellationToken) > 0)
      return Failed<FestivalStation>(FestivalStationFailureReason.StationHasUnfulfilledItems);

    IReadOnlyList<Guid> strandedItemIds = await _orderability.FindItemsStrandedByRemovingStationsAsync(festivalId, [stationId], cancellationToken);

    if (strandedItemIds.Count > 0)
    {
      return Result<FestivalStation, FestivalStationFailure>.Failed(new()
                                                                    {
                                                                      Reason = FestivalStationFailureReason.ItemsWouldHaveNoStation,
                                                                      StrandedItemCount = strandedItemIds.Count
                                                                    });
    }

    IReadOnlyList<ItemStationAssignment> assignmentsHere = await _repository.FindAssignmentsAtStationAsync(festivalId, stationId, cancellationToken);

    _repository.RemoveAssignments(assignmentsHere);
    _repository.RemoveLink(link);

    await _repository.SaveChangesAsync(cancellationToken);

    return Result<FestivalStation, FestivalStationFailure>.Success(link);
  }

  private Result<TValue, FestivalStationFailure> Failed<TValue>(FestivalStationFailureReason reason)
  {
    return Result<TValue, FestivalStationFailure>.Failed(new() { Reason = reason });
  }

  private async Task<Result<TValue, FestivalStationFailure>> RunAsync<TValue>(Func<CancellationToken, Task<Result<TValue, FestivalStationFailure>>> write, Func<Result<TValue, FestivalStationFailure>, bool> shouldCommit, CancellationToken cancellationToken)
  {
    return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                             {
                                               Result<TValue, FestivalStationFailure> written = await write(transactionCancellationToken);

                                               return new TransactionOutcome<Result<TValue, FestivalStationFailure>>
                                                      {
                                                        Value = written,
                                                        ShouldCommit = shouldCommit(written)
                                                      };
                                             },
                                             cancellationToken);
  }
}
