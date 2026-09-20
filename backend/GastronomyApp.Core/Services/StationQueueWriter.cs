using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class StationQueueWriter
{
  private readonly IClock _clock;
  private readonly OrderItemFulfillmentService _fulfillmentService;
  private readonly IStationOrderRepository _repository;
  private readonly ITransactionRunner _transactionRunner;
  private readonly StationOrderVisibilityService _visibilityService;

  public StationQueueWriter(IStationOrderRepository repository, OrderItemFulfillmentService fulfillmentService, StationOrderVisibilityService visibilityService, ITransactionRunner transactionRunner, IClock clock)
  {
    _repository = repository;
    _fulfillmentService = fulfillmentService;
    _visibilityService = visibilityService;
    _transactionRunner = transactionRunner;
    _clock = clock;
  }

  public async Task<Result<FulfillmentResult, StationQueueFailure>> FulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    List<Guid> selectedIds = orderItemIds.ToList();

    return await RunAsync(async transactionCancellationToken =>
                          {
                            IReadOnlyList<OrderItem> itemsAtThisStation = await _repository.FindItemsAtStationAsync(selectedIds, stationId, transactionCancellationToken);

                            return _fulfillmentService.Fulfill(new() { OrderItemIds = selectedIds }, itemsAtThisStation, _clock.UtcNow);
                          },
                          cancellationToken);
  }

  public async Task<Result<FulfillmentResult, StationQueueFailure>> UnfulfillAsync(IReadOnlyCollection<Guid> orderItemIds, Guid stationId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(orderItemIds);

    List<Guid> selectedIds = orderItemIds.ToList();

    return await RunAsync(async transactionCancellationToken =>
                          {
                            IReadOnlyList<OrderItem> itemsAtThisStation = await _repository.FindItemsAtStationAsync(selectedIds, stationId, transactionCancellationToken);

                            return _fulfillmentService.Unfulfill(new() { OrderItemIds = selectedIds }, itemsAtThisStation);
                          },
                          cancellationToken);
  }

  public async Task<Result<StationOrder, StationQueueFailure>> HideFromAsItComesQueueAsync(Guid stationOrderId, Guid stationId, Guid festivalId, CancellationToken cancellationToken)
  {
    var stationOrder = await _repository.FindAtStationAsync(stationOrderId, stationId, festivalId, cancellationToken);

    if (stationOrder is null)
    {
      return Result<StationOrder, StationQueueFailure>.Failed(new() { Reason = StationQueueFailureReason.OrderNotAtThisStation });
    }

    Result<StationOrder, StationOrderVisibilityFailure> hidden = _visibilityService.HideFromAsItComesQueue(stationOrder);

    if (!hidden.IsSuccess)
    {
      return Result<StationOrder, StationQueueFailure>.Failed(new() { Reason = TranslateVisibilityReason(hidden.Failure.Reason) });
    }

    await _repository.SaveChangesAsync(cancellationToken);

    return Result<StationOrder, StationQueueFailure>.Success(hidden.Value);
  }

  private async Task<Result<FulfillmentResult, StationQueueFailure>> RunAsync(Func<CancellationToken, Task<Result<FulfillmentResult, FulfillmentFailure>>> body, CancellationToken cancellationToken)
  {
    Result<FulfillmentResult, FulfillmentFailure> outcome = await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                                                                              {
                                                                                                Result<FulfillmentResult, FulfillmentFailure> applied = await body(transactionCancellationToken);

                                                                                                if (applied.IsSuccess)
                                                                                                  await _repository.SaveChangesAsync(transactionCancellationToken);

                                                                                                return new TransactionOutcome<Result<FulfillmentResult, FulfillmentFailure>>
                                                                                                       {
                                                                                                         Value = applied,
                                                                                                         ShouldCommit = applied.IsSuccess
                                                                                                       };
                                                                                              },
                                                                                              cancellationToken);

    if (outcome.IsSuccess)
      return Result<FulfillmentResult, StationQueueFailure>.Success(outcome.Value);

    return Result<FulfillmentResult, StationQueueFailure>.Failed(new()
                                                                 {
                                                                   Reason = TranslateFulfillmentReason(outcome.Failure.Reason),
                                                                   OffendingOrderItemId = outcome.Failure.OffendingOrderItemId
                                                                 });
  }

  private StationQueueFailureReason TranslateFulfillmentReason(FulfillmentFailureReason reason)
  {
    return reason switch
           {
             FulfillmentFailureReason.NoItemsSelected => StationQueueFailureReason.NoItemsSelected,
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
