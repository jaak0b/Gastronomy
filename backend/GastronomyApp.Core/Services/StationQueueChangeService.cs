using ErrorOr;
using GastronomyApp.Core.Entities;
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

  public Task<ErrorOr<StationQueueChange>> FulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    return ChangedAsync(stationId, station => _writer.FulfillAsync(orderItemIds, stationId, cancellationToken), cancellationToken);
  }

  public Task<ErrorOr<StationQueueChange>> UnfulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    return ChangedAsync(stationId, station => _writer.UnfulfillAsync(orderItemIds, stationId, cancellationToken), cancellationToken);
  }

  public Task<ErrorOr<StationQueueChange>> HideFromAsItComesQueueAsync(Guid stationOrderId, Guid stationId, CancellationToken cancellationToken)
  {
    return ChangedAsync(stationId, station => HiddenAsync(stationOrderId, stationId, station.FestivalId, cancellationToken), cancellationToken);
  }

  private async Task<ErrorOr<IReadOnlyList<StationOrder>>> HiddenAsync(Guid stationOrderId, Guid stationId, Guid festivalId, CancellationToken cancellationToken)
  {
    ErrorOr<StationOrder> hidden = await _writer.HideFromAsItComesQueueAsync(stationOrderId, stationId, festivalId, cancellationToken);

    if (hidden.IsError)
      return hidden.Errors;

    return Array.Empty<StationOrder>().ToErrorOr<IReadOnlyList<StationOrder>>();
  }

  private async Task<ErrorOr<StationQueueChange>> ChangedAsync(Guid stationId, Func<FestivalStation, Task<ErrorOr<IReadOnlyList<StationOrder>>>> write, CancellationToken cancellationToken)
  {
    ErrorOr<FestivalStation> access = await _lookup.FindAsync(stationId, cancellationToken);

    if (access.IsError)
      return access.Errors;

    var station = access.Value;

    ErrorOr<IReadOnlyList<StationOrder>> written = await write(station);

    if (written.IsError)
      return written.Errors;

    return await BuildChangeAsync(station, written.Value, cancellationToken);
  }

  private async Task<ErrorOr<StationQueueChange>> BuildChangeAsync(FestivalStation station, IReadOnlyCollection<StationOrder> touchedStationOrders, CancellationToken cancellationToken)
  {
    IReadOnlyList<Order> changedOrders = await _changedOrderReader.ReadOrdersOfStationOrdersAsync(touchedStationOrders.Select(stationOrder => stationOrder.Id).Distinct().ToList(), cancellationToken);

    ErrorOr<Station> queue = await _queueService.ReadQueueAtAsync(station, cancellationToken);

    return queue.Then(stationWithQueue => new StationQueueChange
                                          {
                                            Station = stationWithQueue,
                                            ChangedOrders = changedOrders
                                          });
  }
}
