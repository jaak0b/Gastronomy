using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Exceptions;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class OrderAcceptanceService
{
  private readonly TimeProvider _timeProvider;
  private readonly IAfterCommitActions _afterCommitActions;
  private readonly IStationOrdersAnnouncer _announcer;
  private readonly OrderItemResolutionService _itemResolutionService;
  private readonly INumberAllocator _numberAllocator;

  private readonly IOrderRepository _orderRepository;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly OrderItemSettlementService _settlementService;
  private readonly ITransactionRunner _transactionRunner;

  public OrderAcceptanceService(IOrderRepository orderRepository,
                                RunningFestivalLookup runningFestival,
                                INumberAllocator numberAllocator,
                                OrderItemResolutionService itemResolutionService,
                                OrderItemSettlementService settlementService,
                                IStationOrdersAnnouncer announcer,
                                IAfterCommitActions afterCommitActions,
                                ITransactionRunner transactionRunner,
                                TimeProvider timeProvider)
  {
    _orderRepository = orderRepository;
    _runningFestival = runningFestival;
    _numberAllocator = numberAllocator;
    _itemResolutionService = itemResolutionService;
    _settlementService = settlementService;
    _announcer = announcer;
    _afterCommitActions = afterCommitActions;
    _transactionRunner = transactionRunner;
    _timeProvider = timeProvider;
  }

  public async Task<ErrorOr<Order>> AcceptAsync(PlaceOrderRequest request, Guid staffMemberId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    try
    {
      return await _transactionRunner.RunAsync(transactionCancellationToken => AcceptInsideTransactionAsync(request, staffMemberId, transactionCancellationToken), cancellationToken);
    }
    catch (ConcurrentWriteException)
    {
      return Refusal.Order.OrderNumberCouldNotBeAllocated();
    }
  }

  private async Task<ErrorOr<Order>> AcceptInsideTransactionAsync(PlaceOrderRequest request, Guid staffMemberId, CancellationToken cancellationToken)
  {
    var existingOrder = await _orderRepository.FindByClientOrderIdAsync(request.ClientOrderId, cancellationToken);
    if (existingOrder is not null)
      return await AcceptedOrderAsync(existingOrder.Id, cancellationToken);

    var festival = await _runningFestival.FindAsync(cancellationToken);
    if (festival is null)
      return Refusal.Order.NoRunningFestival();

    IReadOnlyList<OrderItemRequest> itemRequests = request.Items!;

    ErrorOr<IReadOnlyList<OrderItem>> resolution = await _itemResolutionService.BuildRoutedItemsAsync(festival.Id, itemRequests, cancellationToken);

    if (resolution.IsError)
      return resolution.Errors;

    IReadOnlyList<OrderItem> routedItems = resolution.Value;

    var order = await BuildOrderAsync(request, staffMemberId, festival.Id, routedItems, cancellationToken);

    ErrorOr<Order> settled = SettleAtAcceptance(staffMemberId, order, itemRequests, routedItems);
    if (settled.IsError)
      return settled.Errors;

    await _orderRepository.AddAsync(order, cancellationToken);

    return await AcceptedOrderAsync(order.Id, cancellationToken);
  }

  private async Task<Order> AcceptedOrderAsync(Guid orderId, CancellationToken cancellationToken)
  {
    var storedOrder = (await _orderRepository.FindWithStationOrdersAsync(orderId, cancellationToken))!;

    foreach (var stationId in storedOrder.StationOrders.Select(stationOrder => stationOrder.StationId).Distinct())
      await _afterCommitActions.RunWhenCommittedAsync(announcementCancellationToken => _announcer.AnnounceStationOrdersChangedAsync(stationId, announcementCancellationToken), cancellationToken);

    return storedOrder;
  }

  private ErrorOr<Order> SettleAtAcceptance(Guid staffMemberId, Order order, IReadOnlyList<OrderItemRequest> itemRequests, IReadOnlyList<OrderItem> routedItems)
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
      return order;

    return _settlementService.Settle(lines, staffMemberId, routedItems, order.CreatedAtUtc).Match<ErrorOr<Order>>(settlement => order, settlementErrors => settlementErrors.ConvertAll(Refusal.Order.SettlementCannotBeProcessed));
  }

  private async Task<Order> BuildOrderAsync(PlaceOrderRequest request, Guid staffMemberId, Guid festivalId, IReadOnlyCollection<OrderItem> routedItems, CancellationToken cancellationToken)
  {
    var createdAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
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
