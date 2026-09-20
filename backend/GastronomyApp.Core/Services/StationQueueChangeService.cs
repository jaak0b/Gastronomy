using GastronomyApp.Core.Entities;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class StationQueueChangeService
{
  private readonly StationAtFestivalLookup _lookup;
  private readonly StationQueueService _queueService;
  private readonly OrderStatusReader _statusReader;
  private readonly StationQueueWriter _writer;

  public StationQueueChangeService(StationAtFestivalLookup lookup,
                                   StationQueueWriter writer,
                                   StationQueueService queueService,
                                   OrderStatusReader statusReader)
  {
    _lookup = lookup;
    _writer = writer;
    _queueService = queueService;
    _statusReader = statusReader;
  }

  public async Task<Result<StationQueueChange, StationQueueFailure>> FulfillAsync(
    IReadOnlyCollection<Guid> orderItemIds,
    Guid stationId,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    Result<StationAtFestival, StationQueueFailure> access = await _lookup.FindAsync(stationId, cancellationToken);

    if (!access.IsSuccess)
    {
      return Result<StationQueueChange, StationQueueFailure>.Failed(access.Failure);
    }

    Result<FulfillmentResult, StationQueueFailure> written =
      await _writer.FulfillAsync(orderItemIds, stationId, cancellationToken);

    if (!written.IsSuccess)
    {
      return Result<StationQueueChange, StationQueueFailure>.Failed(written.Failure);
    }

    return await BuildChangeAsync(access.Value,
                                  [.. written.Value.ChangedItems, .. written.Value.AlreadyFulfilled],
                                  cancellationToken);
  }

  public async Task<Result<StationQueueChange, StationQueueFailure>> UnfulfillAsync(
    IReadOnlyCollection<Guid> orderItemIds,
    Guid stationId,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    Result<StationAtFestival, StationQueueFailure> access = await _lookup.FindAsync(stationId, cancellationToken);

    if (!access.IsSuccess)
    {
      return Result<StationQueueChange, StationQueueFailure>.Failed(access.Failure);
    }

    Result<FulfillmentResult, StationQueueFailure> written =
      await _writer.UnfulfillAsync(orderItemIds, stationId, cancellationToken);

    if (!written.IsSuccess)
    {
      return Result<StationQueueChange, StationQueueFailure>.Failed(written.Failure);
    }

    return await BuildChangeAsync(access.Value, written.Value.ChangedItems, cancellationToken);
  }

  public async Task<Result<StationQueueChange, StationQueueFailure>> HideFromAsItComesQueueAsync(
    Guid stationOrderId,
    Guid stationId,
    CancellationToken cancellationToken)
  {
    Result<StationAtFestival, StationQueueFailure> access = await _lookup.FindAsync(stationId, cancellationToken);

    if (!access.IsSuccess)
    {
      return Result<StationQueueChange, StationQueueFailure>.Failed(access.Failure);
    }

    Result<StationOrder, StationQueueFailure> hidden =
      await _writer.HideFromAsItComesQueueAsync(stationOrderId,
                                                stationId,
                                                access.Value.FestivalId,
                                                cancellationToken);

    if (!hidden.IsSuccess)
    {
      return Result<StationQueueChange, StationQueueFailure>.Failed(hidden.Failure);
    }

    return await BuildChangeAsync(access.Value, [], cancellationToken);
  }

  private async Task<Result<StationQueueChange, StationQueueFailure>> BuildChangeAsync(
    StationAtFestival station,
    IReadOnlyCollection<OrderItem> touchedItems,
    CancellationToken cancellationToken)
  {
    IReadOnlyList<OrderStatusChange> statusChanges =
      await _statusReader.ReadStatusesOfStationOrdersAsync([.. touchedItems.Select(item => item.StationOrderId)
                                                                          .Distinct()],
                                                           cancellationToken);

    StationQueue queue = await _queueService.ReadQueueAtAsync(station, cancellationToken);

    return Result<StationQueueChange, StationQueueFailure>.Success(new()
                                                                  {
                                                                    Queue = queue,
                                                                    OrderStatusChanges = statusChanges
                                                                  });
  }
}
