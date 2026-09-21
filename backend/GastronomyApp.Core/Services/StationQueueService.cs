using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
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

  public async Task<Result<StationQueue, StationQueueFailure>> ReadQueueAsync(Guid stationId, CancellationToken cancellationToken)
  {
    Result<StationAtFestival, StationQueueFailure> access = await _lookup.FindAsync(stationId, cancellationToken);

    if (!access.IsSuccess)
      return Result<StationQueue, StationQueueFailure>.Failed(access.Failure);

    return Result<StationQueue, StationQueueFailure>.Success(await ReadQueueAtAsync(access.Value, cancellationToken));
  }

  public async Task<StationQueue> ReadQueueAtAsync(StationAtFestival station, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(station);

    IReadOnlyList<QueuedStationOrder> orders = await _repository.FindUnfinishedAtStationAsync(station.FestivalId, station.Station.Id, cancellationToken);

    return new()
           {
             StationId = station.Station.Id,
             StationName = station.Station.Name,
             Orders = orders,
             AsItComesOrders = orders.Where(stationOrder => stationOrder.DeliveryMode == DeliveryMode.AsItComes && !stationOrder.IsHiddenFromAsItComesQueue).ToList()
           };
  }

  public async Task<Result<IReadOnlyList<QueuedStationOrder>, StationQueueFailure>> ReadFulfilledAsync(Guid stationId, CancellationToken cancellationToken)
  {
    Result<StationAtFestival, StationQueueFailure> access = await _lookup.FindAsync(stationId, cancellationToken);

    if (!access.IsSuccess)
      return Result<IReadOnlyList<QueuedStationOrder>, StationQueueFailure>.Failed(access.Failure);

    IReadOnlyList<QueuedStationOrder> stationOrders = await _repository.FindFulfilledAtStationAsync(access.Value.FestivalId, access.Value.Station.Id, cancellationToken);

    return Result<IReadOnlyList<QueuedStationOrder>, StationQueueFailure>.Success(stationOrders);
  }
}
