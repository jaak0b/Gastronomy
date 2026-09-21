using GastronomyApp.Core.Entities;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class StationQueueChangeService
{
  private readonly StationAtFestivalLookup _lookup;
  private readonly StationQueueService _queueService;
  private readonly ChangedOrderReader _changedOrderReader;
  private readonly StationQueueWriter _writer;

  public StationQueueChangeService(StationAtFestivalLookup lookup, StationQueueWriter writer, StationQueueService queueService, ChangedOrderReader changedOrderReader)
  {
    _lookup = lookup;
    _writer = writer;
    _queueService = queueService;
    _changedOrderReader = changedOrderReader;
  }

  public async Task<Result<StationQueueChange, StationQueueFailure>> FulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    Result<StationAtFestival, StationQueueFailure> access = await _lookup.FindAsync(stationId, cancellationToken);

    if (!access.IsSuccess)
      return Result<StationQueueChange, StationQueueFailure>.Failed(access.Failure);

    Result<IReadOnlyList<StationOrder>, StationQueueFailure> written = await _writer.FulfillAsync(orderItemIds, stationId, cancellationToken);

    if (!written.IsSuccess)
      return Result<StationQueueChange, StationQueueFailure>.Failed(written.Failure);

    return await BuildChangeAsync(access.Value, written.Value, cancellationToken);
  }

  public async Task<Result<StationQueueChange, StationQueueFailure>> UnfulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    Result<StationAtFestival, StationQueueFailure> access = await _lookup.FindAsync(stationId, cancellationToken);

    if (!access.IsSuccess)
      return Result<StationQueueChange, StationQueueFailure>.Failed(access.Failure);

    Result<IReadOnlyList<StationOrder>, StationQueueFailure> written = await _writer.UnfulfillAsync(orderItemIds, stationId, cancellationToken);

    if (!written.IsSuccess)
      return Result<StationQueueChange, StationQueueFailure>.Failed(written.Failure);

    return await BuildChangeAsync(access.Value, written.Value, cancellationToken);
  }

  public async Task<Result<StationQueueChange, StationQueueFailure>> HideFromAsItComesQueueAsync(Guid stationOrderId, Guid stationId, CancellationToken cancellationToken)
  {
    Result<StationAtFestival, StationQueueFailure> access = await _lookup.FindAsync(stationId, cancellationToken);

    if (!access.IsSuccess)
      return Result<StationQueueChange, StationQueueFailure>.Failed(access.Failure);

    Result<StationOrder, StationQueueFailure> hidden = await _writer.HideFromAsItComesQueueAsync(stationOrderId, stationId, access.Value.FestivalId, cancellationToken);

    if (!hidden.IsSuccess)
      return Result<StationQueueChange, StationQueueFailure>.Failed(hidden.Failure);

    return await BuildChangeAsync(access.Value, [], cancellationToken);
  }

  private async Task<Result<StationQueueChange, StationQueueFailure>> BuildChangeAsync(StationAtFestival station, IReadOnlyCollection<StationOrder> touchedStationOrders, CancellationToken cancellationToken)
  {
    IReadOnlyList<Order> changedOrders = await _changedOrderReader.ReadOrdersOfStationOrdersAsync(touchedStationOrders.Select(stationOrder => stationOrder.Id).Distinct().ToList(), cancellationToken);

    Result<Station, StationQueueFailure> queue = await _queueService.ReadQueueAtAsync(station, cancellationToken);

    if (!queue.IsSuccess)
      return Result<StationQueueChange, StationQueueFailure>.Failed(queue.Failure);

    return Result<StationQueueChange, StationQueueFailure>.Success(new()
                                                                   {
                                                                     Station = queue.Value,
                                                                     ChangedOrders = changedOrders
                                                                   });
  }
}
