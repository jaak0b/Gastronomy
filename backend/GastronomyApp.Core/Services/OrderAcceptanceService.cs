using GastronomyApp.Contracts;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Exceptions;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class OrderAcceptanceService
{
  private readonly IClock _clock;
  private readonly OrderItemResolutionService _itemResolutionService;
  private readonly INumberAllocator _numberAllocator;

  private readonly IOrderRepository _orderRepository;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly OrderItemSettlementService _settlementService;
  private readonly ITransactionRunner _transactionRunner;

  public OrderAcceptanceService(IOrderRepository orderRepository, RunningFestivalLookup runningFestival, INumberAllocator numberAllocator, OrderItemResolutionService itemResolutionService, OrderItemSettlementService settlementService, ITransactionRunner transactionRunner, IClock clock)
  {
    _orderRepository = orderRepository;
    _runningFestival = runningFestival;
    _numberAllocator = numberAllocator;
    _itemResolutionService = itemResolutionService;
    _settlementService = settlementService;
    _transactionRunner = transactionRunner;
    _clock = clock;
  }

  public async Task<Result<Order, OrderValidationFailure>> AcceptAsync(PlaceOrderRequest request, Guid staffMemberId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    try
    {
      return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                               {
                                                 Result<Order, OrderValidationFailure> acceptance = await AcceptInsideTransactionAsync(request, staffMemberId, transactionCancellationToken);

                                                 return new TransactionOutcome<Result<Order, OrderValidationFailure>>
                                                        {
                                                          Value = acceptance,
                                                          ShouldCommit = acceptance.IsSuccess
                                                        };
                                               },
                                               cancellationToken);
    }
    catch (ConcurrentWriteException)
    {
      return Result<Order, OrderValidationFailure>.Failed(new() { Reason = OrderValidationFailureReason.OrderNumberCouldNotBeAllocated });
    }
  }

  private async Task<Result<Order, OrderValidationFailure>> AcceptInsideTransactionAsync(PlaceOrderRequest request, Guid staffMemberId, CancellationToken cancellationToken)
  {
    var shapeFailure = ValidateShape(request);
    if (shapeFailure is not null)
      return Result<Order, OrderValidationFailure>.Failed(shapeFailure);

    var existingOrder = await _orderRepository.FindByClientOrderIdAsync(request.ClientOrderId, cancellationToken);
    if (existingOrder is not null)
      return Result<Order, OrderValidationFailure>.Success(existingOrder);

    var festival = await _runningFestival.FindAsync(cancellationToken);
    if (festival is null)
      return Result<Order, OrderValidationFailure>.Failed(new() { Reason = OrderValidationFailureReason.NoRunningFestival });

    IReadOnlyList<OrderItemRequest> itemRequests = request.Items!;

    Result<IReadOnlyList<OrderItem>, OrderValidationFailure> resolution = await _itemResolutionService.BuildRoutedItemsAsync(festival.Id, itemRequests, cancellationToken);

    if (!resolution.IsSuccess)
      return Result<Order, OrderValidationFailure>.Failed(resolution.Failure);

    IReadOnlyList<OrderItem> routedItems = resolution.Value;

    var order = await BuildOrderAsync(request, staffMemberId, festival.Id, routedItems, cancellationToken);

    var settlementFailure = SettleAtAcceptance(staffMemberId, order, itemRequests, routedItems);
    if (settlementFailure is not null)
      return Result<Order, OrderValidationFailure>.Failed(settlementFailure);

    await _orderRepository.AddAsync(order, cancellationToken);

    return Result<Order, OrderValidationFailure>.Success(order);
  }

  private OrderValidationFailure? ValidateShape(PlaceOrderRequest request)
  {
    if ((request.Items?.Count ?? 0) == 0)
      return new() { Reason = OrderValidationFailureReason.NoItems };

    if (string.IsNullOrWhiteSpace(request.TableName))
      return new() { Reason = OrderValidationFailureReason.TableNameMissing };

    foreach (var item in request.Items!)
      if (item.UnitPriceCents < 0)
      {
        return new()
               {
                 Reason = OrderValidationFailureReason.PriceOutOfRange,
                 OffendingCatalogItemId = item.CatalogItemId
               };
      }

    return null;
  }

  private OrderValidationFailure? SettleAtAcceptance(Guid staffMemberId, Order order, IReadOnlyList<OrderItemRequest> itemRequests, IReadOnlyList<OrderItem> routedItems)
  {
    List<SettleLineRequest> lines = [];

    for (var index = 0; index < routedItems.Count; index++)
    {
      var settlement = itemRequests[index].Settlement;

      if (settlement is null)
        continue;

      lines.Add(new()
                {
                  OrderItemId = routedItems[index].Id,
                  PaidPriceCents = settlement.PaidPriceCents,
                  PaymentNotice = settlement.PaymentNotice
                });
    }

    if (lines.Count == 0)
      return null;

    Result<SettlementResult, SettlementFailure> settlementResult = _settlementService.Settle(lines, staffMemberId, routedItems, order.CreatedAtUtc);

    if (settlementResult.IsSuccess)
      return null;

    return new()
           {
             Reason = OrderValidationFailureReason.SettlementCannotBeProcessed,
             SettlementFailureReason = settlementResult.Failure.Reason
           };
  }

  private async Task<Order> BuildOrderAsync(PlaceOrderRequest request, Guid staffMemberId, Guid festivalId, IReadOnlyCollection<OrderItem> routedItems, CancellationToken cancellationToken)
  {
    var createdAtUtc = _clock.UtcNow;
    var globalOrderNumber = await _numberAllocator.AllocateGlobalOrderNumberAsync(festivalId, cancellationToken);

    Order order = new()
                  {
                    Id = Guid.NewGuid(),
                    ClientOrderId = request.ClientOrderId,
                    FestivalId = festivalId,
                    GlobalOrderNumber = globalOrderNumber,
                    StaffMemberId = staffMemberId,
                    TableName = request.TableName ?? string.Empty,
                    CreatedAtUtc = createdAtUtc
                  };

    Dictionary<Guid, DeliveryMode> deliveryModesByStationId = (request.DeliveryModes ?? []).GroupBy(mode => mode.StationId).ToDictionary(group => group.Key, group => group.Last().DeliveryMode);

    foreach (var routedItem in routedItems)
    {
      var stationOrder = routedItem.StationOrder;

      if (order.StationOrders.Contains(stationOrder))
        continue;

      stationOrder.OrderId = order.Id;
      stationOrder.Order = order;
      stationOrder.StationOrderNumber = await _numberAllocator.AllocateStationOrderNumberAsync(festivalId, stationOrder.StationId, cancellationToken);
      stationOrder.DeliveryMode = DeliveryModeFor(deliveryModesByStationId, stationOrder.StationId);

      order.StationOrders.Add(stationOrder);
    }

    return order;
  }

  private DeliveryMode DeliveryModeFor(IReadOnlyDictionary<Guid, DeliveryMode> deliveryModesByStationId, Guid stationId)
  {
    if (deliveryModesByStationId.TryGetValue(stationId, out var chosenMode))
      return chosenMode;

    return DeliveryMode.Together;
  }
}
