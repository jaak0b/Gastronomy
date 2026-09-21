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
  private readonly ITransactionRunner _transactionRunner;
  private readonly StationOrderVisibilityService _visibilityService;

  public StationQueueWriter(IStationOrderRepository repository, OrderItemFulfillmentService fulfillmentService, StationOrderVisibilityService visibilityService, ITransactionRunner transactionRunner, TimeProvider timeProvider)
  {
    _repository = repository;
    _fulfillmentService = fulfillmentService;
    _visibilityService = visibilityService;
    _transactionRunner = transactionRunner;
    _timeProvider = timeProvider;
  }

  public Task<ErrorOr<IReadOnlyList<StationOrder>>> FulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    List<Guid> selectedIds = orderItemIds.ToList();

    return RunAsync(async transactionCancellationToken =>
                    {
                      IReadOnlyList<OrderItem> itemsAtThisStation = await _repository.FindItemsAtStationAsync(selectedIds, stationId, transactionCancellationToken);

                      return _fulfillmentService.Fulfill(selectedIds, itemsAtThisStation, _timeProvider.GetUtcNow().UtcDateTime);
                    },
                    cancellationToken);
  }

  public Task<ErrorOr<IReadOnlyList<StationOrder>>> UnfulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    List<Guid> selectedIds = orderItemIds.ToList();

    return RunAsync(async transactionCancellationToken =>
                    {
                      IReadOnlyList<OrderItem> itemsAtThisStation = await _repository.FindItemsAtStationAsync(selectedIds, stationId, transactionCancellationToken);

                      return _fulfillmentService.Unfulfill(selectedIds, itemsAtThisStation);
                    },
                    cancellationToken);
  }

  public async Task<ErrorOr<StationOrder>> HideFromAsItComesQueueAsync(Guid stationOrderId, Guid stationId, Guid festivalId, CancellationToken cancellationToken)
  {
    var stationOrder = await _repository.FindAtStationAsync(stationOrderId, stationId, festivalId, cancellationToken);

    if (stationOrder is null)
      return Refusal.StationQueue.OrderNotAtThisStation(stationOrderId);

    return await _visibilityService.HideFromAsItComesQueue(stationOrder)
                                   .ThenDoAsync(hidden => _repository.SaveChangesAsync(cancellationToken));
  }

  private Task<ErrorOr<IReadOnlyList<StationOrder>>> RunAsync(Func<CancellationToken, Task<ErrorOr<IReadOnlyList<StationOrder>>>> body, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => body(transactionCancellationToken).ThenDoAsync(applied => _repository.SaveChangesAsync(transactionCancellationToken)),
                                       cancellationToken);
  }
}
