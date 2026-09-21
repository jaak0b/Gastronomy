using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class StationQueueService
{
  private readonly StationAtFestivalLookup _lookup;
  private readonly IStationOrderRepository _repository;

  public StationQueueService(StationAtFestivalLookup lookup, IStationOrderRepository repository)
  {
    _lookup = lookup;
    _repository = repository;
  }

  public async Task<Result<Station, StationQueueFailure>> ReadQueueAsync(Guid stationId, CancellationToken cancellationToken)
  {
    Result<FestivalStation, StationQueueFailure> access = await _lookup.FindAsync(stationId, cancellationToken);

    if (!access.IsSuccess)
      return Result<Station, StationQueueFailure>.Failed(access.Failure);

    return await ReadQueueAtAsync(access.Value, cancellationToken);
  }

  public async Task<Result<Station, StationQueueFailure>> ReadQueueAtAsync(FestivalStation station, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(station);

    var withQueue = await _repository.FindStationWithUnfinishedOrdersAsync(station.FestivalId, station.StationId, cancellationToken);

    if (withQueue is null)
      return Result<Station, StationQueueFailure>.Failed(new() { Reason = StationQueueFailureReason.StationUnknown });

    return Result<Station, StationQueueFailure>.Success(withQueue);
  }

  public async Task<Result<IReadOnlyList<StationOrder>, StationQueueFailure>> ReadFulfilledAsync(Guid stationId, CancellationToken cancellationToken)
  {
    Result<FestivalStation, StationQueueFailure> access = await _lookup.FindAsync(stationId, cancellationToken);

    if (!access.IsSuccess)
      return Result<IReadOnlyList<StationOrder>, StationQueueFailure>.Failed(access.Failure);

    IReadOnlyList<StationOrder> stationOrders = await _repository.FindFulfilledAtStationAsync(access.Value.FestivalId, access.Value.StationId, cancellationToken);

    return Result<IReadOnlyList<StationOrder>, StationQueueFailure>.Success(stationOrders);
  }
}
