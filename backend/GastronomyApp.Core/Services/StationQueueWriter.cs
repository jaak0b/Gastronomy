using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Services;

public sealed class StationQueueWriter
{
  private readonly TimeProvider _timeProvider;
  private readonly OrderItemFulfillmentService _fulfillmentService;
  private readonly IStationOrderRepository _repository;

  public StationQueueWriter(IStationOrderRepository repository, OrderItemFulfillmentService fulfillmentService, TimeProvider timeProvider)
  {
    _repository = repository;
    _fulfillmentService = fulfillmentService;
    _timeProvider = timeProvider;
  }

  public async Task<ErrorOr<IReadOnlyList<StationOrder>>> FulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    List<Guid> selectedIds = orderItemIds.ToList();

    IReadOnlyList<OrderItem> itemsAtThisStation = await _repository.FindItemsAtStationAsync(selectedIds, stationId, cancellationToken);

    return await _fulfillmentService.Fulfill(selectedIds, itemsAtThisStation, _timeProvider.GetUtcNow().UtcDateTime).ThenDoAsync(applied => _repository.SaveChangesAsync(cancellationToken));
  }

  public async Task<ErrorOr<IReadOnlyList<StationOrder>>> UnfulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    List<Guid> selectedIds = orderItemIds.ToList();

    IReadOnlyList<OrderItem> itemsAtThisStation = await _repository.FindItemsAtStationAsync(selectedIds, stationId, cancellationToken);

    return await _fulfillmentService.Unfulfill(selectedIds, itemsAtThisStation).ThenDoAsync(applied => _repository.SaveChangesAsync(cancellationToken));
  }

  public async Task<ErrorOr<StationOrder>> HideFromAsItComesQueueAsync(Guid stationOrderId, Guid stationId, Guid festivalId, CancellationToken cancellationToken)
  {
    var stationOrder = await _repository.FindAtStationAsync(stationOrderId, stationId, festivalId, cancellationToken);

    if (stationOrder is null)
      return Refusal.StationQueue.OrderNotAtThisStation(stationOrderId);

    return await stationOrder.HideFromAsItComesQueue().ThenDoAsync(hidden => _repository.SaveChangesAsync(cancellationToken));
  }
}
