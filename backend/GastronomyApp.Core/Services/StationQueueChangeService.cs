using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Services;

public sealed class StationQueueChangeService
{
  private readonly IAfterCommitActions _afterCommitActions;
  private readonly ChangedOrderReader _changedOrderReader;
  private readonly StationAtFestivalLookup _lookup;
  private readonly IOrderStatusAnnouncer _orderStatusAnnouncer;
  private readonly StationQueueService _queueService;
  private readonly IStationOrdersAnnouncer _stationOrdersAnnouncer;
  private readonly StationQueueWriter _writer;

  public StationQueueChangeService(StationAtFestivalLookup lookup, StationQueueWriter writer, StationQueueService queueService, ChangedOrderReader changedOrderReader, IStationOrdersAnnouncer stationOrdersAnnouncer, IOrderStatusAnnouncer orderStatusAnnouncer, IAfterCommitActions afterCommitActions)
  {
    _lookup = lookup;
    _writer = writer;
    _queueService = queueService;
    _changedOrderReader = changedOrderReader;
    _stationOrdersAnnouncer = stationOrdersAnnouncer;
    _orderStatusAnnouncer = orderStatusAnnouncer;
    _afterCommitActions = afterCommitActions;
  }

  public Task<ErrorOr<Station>> FulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    return ChangedAsync(stationId, station => _writer.FulfillAsync(orderItemIds, stationId, cancellationToken), cancellationToken);
  }

  public Task<ErrorOr<Station>> UnfulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    return ChangedAsync(stationId, station => _writer.UnfulfillAsync(orderItemIds, stationId, cancellationToken), cancellationToken);
  }

  public Task<ErrorOr<Station>> HideFromAsItComesQueueAsync(Guid stationOrderId, Guid stationId, CancellationToken cancellationToken)
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

  private async Task<ErrorOr<Station>> ChangedAsync(Guid stationId, Func<FestivalStation, Task<ErrorOr<IReadOnlyList<StationOrder>>>> write, CancellationToken cancellationToken)
  {
    ErrorOr<FestivalStation> access = await _lookup.FindAsync(stationId, cancellationToken);

    if (access.IsError)
      return access.Errors;

    var station = access.Value;

    ErrorOr<IReadOnlyList<StationOrder>> written = await write(station);

    if (written.IsError)
      return written.Errors;

    return await AnnouncedQueueAsync(station, written.Value, cancellationToken);
  }

  private async Task<ErrorOr<Station>> AnnouncedQueueAsync(FestivalStation station, IReadOnlyCollection<StationOrder> touchedStationOrders, CancellationToken cancellationToken)
  {
    IReadOnlyList<Order> changedOrders = await _changedOrderReader.ReadOrdersOfStationOrdersAsync(touchedStationOrders.Select(stationOrder => stationOrder.Id).Distinct().ToList(), cancellationToken);

    ErrorOr<Station> queue = await _queueService.ReadQueueAtAsync(station, cancellationToken);

    if (queue.IsError)
      return queue.Errors;

    await _afterCommitActions.RunWhenCommittedAsync(announcementCancellationToken => _stationOrdersAnnouncer.AnnounceStationOrdersChangedAsync(station.StationId, announcementCancellationToken), cancellationToken);

    foreach (var changedOrder in changedOrders)
      await _afterCommitActions.RunWhenCommittedAsync(announcementCancellationToken => _orderStatusAnnouncer.AnnounceOrderStatusChangedAsync(changedOrder, announcementCancellationToken), cancellationToken);

    return queue;
  }
}
