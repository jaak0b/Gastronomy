using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

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

  public Task<ErrorOr<FestivalStation>> AddAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => AddedAsync(festivalId, stationId, transactionCancellationToken), cancellationToken);
  }

  public Task<ErrorOr<FestivalStation>> RemoveAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => RemovedAsync(festivalId, stationId, transactionCancellationToken), cancellationToken);
  }

  private async Task<ErrorOr<FestivalStation>> AddedAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    if (!await _festivalRepository.ExistsAsync(festivalId, cancellationToken))
      return Refusal.FestivalStation.FestivalNotFound(festivalId);

    if (!await _stationRepository.ExistsAsync(stationId, cancellationToken))
      return Refusal.FestivalStation.StationNotFound(stationId);

    if (await _repository.FindLinkAsync(festivalId, stationId, cancellationToken) is { } alreadyTakingPart)
      return alreadyTakingPart;

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

    return link;
  }

  private async Task<ErrorOr<FestivalStation>> RemovedAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    var link = await _repository.FindLinkAsync(festivalId, stationId, cancellationToken);

    if (link is null)
      return Refusal.FestivalStation.StationLinkNotFound(festivalId, stationId);

    var festival = await _festivalRepository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Refusal.FestivalStation.FestivalNotFound(festivalId);

    if (_runningFestival.IsRunning(festival) && await _repository.CountUnfulfilledItemsAsync(festivalId, stationId, cancellationToken) > 0)
      return Refusal.FestivalStation.StationHasUnfulfilledItems(festivalId, stationId);

    IReadOnlyList<Guid> strandedItemIds = await _orderability.FindItemsStrandedByRemovingStationsAsync(festivalId, [stationId], cancellationToken);

    if (strandedItemIds.Count > 0)
      return Refusal.FestivalStation.ItemsWouldHaveNoStation(strandedItemIds.Count);

    IReadOnlyList<ItemStationAssignment> assignmentsHere = await _repository.FindAssignmentsAtStationAsync(festivalId, stationId, cancellationToken);

    _repository.RemoveAssignments(assignmentsHere);
    _repository.RemoveLink(link);

    await _repository.SaveChangesAsync(cancellationToken);

    return link;
  }
}
