using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class OrderAcceptanceService
{
  private readonly TimeProvider _timeProvider;
  private readonly OrderItemResolutionService _itemResolutionService;
  private readonly INumberAllocator _numberAllocator;

  private readonly IOrderRepository _orderRepository;
  private readonly RunningFestivalLookup _runningFestival;

  public OrderAcceptanceService(IOrderRepository orderRepository,
                                RunningFestivalLookup runningFestival,
                                INumberAllocator numberAllocator,
                                OrderItemResolutionService itemResolutionService,
                                TimeProvider timeProvider)
  {
    _orderRepository = orderRepository;
    _runningFestival = runningFestival;
    _numberAllocator = numberAllocator;
    _itemResolutionService = itemResolutionService;
    _timeProvider = timeProvider;
  }

  public async Task<ErrorOr<Order>> AcceptAsync(PlaceOrderRequest request, Guid staffMemberId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

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

    await _orderRepository.AddAsync(order, cancellationToken);

    return await AcceptedOrderAsync(order.Id, cancellationToken);
  }

  private async Task<Order> AcceptedOrderAsync(Guid orderId, CancellationToken cancellationToken)
  {
    return (await _orderRepository.FindWithStationOrdersAsync(orderId, cancellationToken))!;
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
