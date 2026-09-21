using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

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

  public async Task<Result<IReadOnlyList<StationOrder>, StationQueueFailure>> FulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    List<Guid> selectedIds = orderItemIds.ToList();

    return await RunAsync(async transactionCancellationToken =>
                          {
                            IReadOnlyList<OrderItem> itemsAtThisStation = await _repository.FindItemsAtStationAsync(selectedIds, stationId, transactionCancellationToken);

                            return _fulfillmentService.Fulfill(selectedIds, itemsAtThisStation, _timeProvider.GetUtcNow().UtcDateTime);
                          },
                          cancellationToken);
  }

  public async Task<Result<IReadOnlyList<StationOrder>, StationQueueFailure>> UnfulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    List<Guid> selectedIds = orderItemIds.ToList();

    return await RunAsync(async transactionCancellationToken =>
                          {
                            IReadOnlyList<OrderItem> itemsAtThisStation = await _repository.FindItemsAtStationAsync(selectedIds, stationId, transactionCancellationToken);

                            return _fulfillmentService.Unfulfill(selectedIds, itemsAtThisStation);
                          },
                          cancellationToken);
  }

  public async Task<Result<StationOrder, StationQueueFailure>> HideFromAsItComesQueueAsync(Guid stationOrderId, Guid stationId, Guid festivalId, CancellationToken cancellationToken)
  {
    var stationOrder = await _repository.FindAtStationAsync(stationOrderId, stationId, festivalId, cancellationToken);

    if (stationOrder is null)
      return Result<StationOrder, StationQueueFailure>.Failed(new() { Reason = StationQueueFailureReason.OrderNotAtThisStation });

    Result<StationOrder, Failure<StationOrderVisibilityFailureReason>> hidden = _visibilityService.HideFromAsItComesQueue(stationOrder);

    if (!hidden.IsSuccess)
      return Result<StationOrder, StationQueueFailure>.Failed(new() { Reason = TranslateVisibilityReason(hidden.Failure.Reason) });

    await _repository.SaveChangesAsync(cancellationToken);

    return Result<StationOrder, StationQueueFailure>.Success(hidden.Value);
  }

  private async Task<Result<IReadOnlyList<StationOrder>, StationQueueFailure>> RunAsync(Func<CancellationToken, Task<Result<IReadOnlyList<StationOrder>, FulfillmentFailure>>> body, CancellationToken cancellationToken)
  {
    Result<IReadOnlyList<StationOrder>, FulfillmentFailure> outcome = await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                                                                                        {
                                                                                                          Result<IReadOnlyList<StationOrder>, FulfillmentFailure> applied = await body(transactionCancellationToken);

                                                                                                          if (applied.IsSuccess)
                                                                                                            await _repository.SaveChangesAsync(transactionCancellationToken);

                                                                                                          return new TransactionOutcome<Result<IReadOnlyList<StationOrder>, FulfillmentFailure>>
                                                                                                                 {
                                                                                                                   Value = applied,
                                                                                                                   ShouldCommit = applied.IsSuccess
                                                                                                                 };
                                                                                                        },
                                                                                                        cancellationToken);

    if (outcome.IsSuccess)
      return Result<IReadOnlyList<StationOrder>, StationQueueFailure>.Success(outcome.Value);

    return Result<IReadOnlyList<StationOrder>, StationQueueFailure>.Failed(new()
                                                                           {
                                                                             Reason = TranslateFulfillmentReason(outcome.Failure.Reason),
                                                                             OffendingOrderItemId = outcome.Failure.OffendingOrderItemId
                                                                           });
  }

  private StationQueueFailureReason TranslateFulfillmentReason(FulfillmentFailureReason reason)
  {
    return reason switch
           {
             FulfillmentFailureReason.UnknownOrderItemId => StationQueueFailureReason.UnknownOrderItemId,
             FulfillmentFailureReason.ItemNotFulfilled => StationQueueFailureReason.ItemNotFulfilled,
             _ => new UnreachableCase().Throw<StationQueueFailureReason>(reason)
           };
  }

  private StationQueueFailureReason TranslateVisibilityReason(StationOrderVisibilityFailureReason reason)
  {
    return reason switch
           {
             StationOrderVisibilityFailureReason.NotAnAsItComesOrder => StationQueueFailureReason.NotAnAsItComesOrder,
             _ => new UnreachableCase().Throw<StationQueueFailureReason>(reason)
           };
  }
}
