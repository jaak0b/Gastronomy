using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class OrderAcceptanceService
{
  private readonly ICatalogItemRepository _catalogItemRepository;
  private readonly IClock _clock;
  private readonly IFestivalRepository _festivalRepository;
  private readonly INumberAllocator _numberAllocator;

  private readonly IOrderRepository _orderRepository;
  private readonly OrderRoutingResolver _routingResolver;
  private readonly OrderItemSettlementService _settlementService;
  private readonly IStationRepository _stationRepository;

  public OrderAcceptanceService(IOrderRepository orderRepository,
                                ICatalogItemRepository catalogItemRepository,
                                IStationRepository stationRepository,
                                IFestivalRepository festivalRepository,
                                INumberAllocator numberAllocator,
                                OrderRoutingResolver routingResolver,
                                OrderItemSettlementService settlementService,
                                IClock clock)
  {
    _orderRepository = orderRepository;
    _catalogItemRepository = catalogItemRepository;
    _stationRepository = stationRepository;
    _festivalRepository = festivalRepository;
    _numberAllocator = numberAllocator;
    _routingResolver = routingResolver;
    _settlementService = settlementService;
    _clock = clock;
  }

  public async Task<Result<OrderAcceptanceResult, OrderValidationFailure>> AcceptAsync(OrderAcceptanceRequest request,
                                                                                       CancellationToken cancellationToken)
  {
    var shapeFailure = ValidateShape(request);
    if (shapeFailure is not null)
    {
      return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(shapeFailure);
    }

    var existingOrder =
      await _orderRepository.FindByClientOrderIdAsync(request.ClientOrderId, cancellationToken);
    if (existingOrder is not null)
    {
      return Result<OrderAcceptanceResult, OrderValidationFailure>.Success(new()
                                                                           {
                                                                             Order = existingOrder,
                                                                             WasAlreadyAccepted = true
                                                                           });
    }

    var festival = await _festivalRepository.FindRunningAsync(_clock.UtcNow, cancellationToken);
    if (festival is null)
    {
      return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(new()
                                                                          {
                                                                            Reason = OrderValidationFailureReason.NoRunningFestival
                                                                          });
    }

    IReadOnlyCollection<Station> stationsAtTheFestival =
      await _stationRepository.FindAtFestivalAsync(festival.Id, cancellationToken);

    List<ResolvedItem> resolvedItems = [];
    foreach (var itemRequest in request.Items)
    {
      var catalogItem =
        await _catalogItemRepository.FindByIdAsync(itemRequest.CatalogItemId, cancellationToken);
      if (catalogItem is null)
      {
        return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(new()
                                                                            {
                                                                              Reason = OrderValidationFailureReason.UnknownCatalogItemId,
                                                                              OffendingCatalogItemId = itemRequest.CatalogItemId
                                                                            });
      }

      var menuRow =
        await _catalogItemRepository.FindMenuRowAsync(festival.Id, itemRequest.CatalogItemId, cancellationToken);
      if (!catalogItem.IsActive || menuRow is { IsAvailable: false })
      {
        return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(new()
                                                                            {
                                                                              Reason = OrderValidationFailureReason.ItemNotAvailable,
                                                                              OffendingCatalogItemId = itemRequest.CatalogItemId,
                                                                              OffendingCatalogItemName = catalogItem.Name
                                                                            });
      }

      IReadOnlyCollection<ItemStationAssignment> assignments =
        await _catalogItemRepository.FindAssignmentsAsync(festival.Id, itemRequest.CatalogItemId, cancellationToken);

      Result<RoutingDecision, RoutingFailure> routing = _routingResolver.Resolve(itemRequest.CatalogItemId,
                                                                                 assignments,
                                                                                 stationsAtTheFestival,
                                                                                 itemRequest.StationId);

      if (!routing.IsSuccess)
      {
        return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(new()
                                                                            {
                                                                              Reason = TranslateRoutingFailureReason(routing.Failure.Reason),
                                                                              OffendingCatalogItemId = itemRequest.CatalogItemId,
                                                                              OffendingCatalogItemName = catalogItem.Name
                                                                            });
      }

      resolvedItems.Add(new()
                        {
                          Request = itemRequest,
                          CatalogItem = catalogItem,
                          Decision = routing.Value
                        });
    }

    var builtOrder = await BuildOrderAsync(request, festival.Id, resolvedItems, cancellationToken);

    var settlementFailure = SettleAtAcceptance(request.StaffMemberId, builtOrder);
    if (settlementFailure is not null)
    {
      return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(settlementFailure);
    }

    await _orderRepository.AddAsync(builtOrder.Order, cancellationToken);

    return Result<OrderAcceptanceResult, OrderValidationFailure>.Success(new()
                                                                         {
                                                                           Order = builtOrder.Order,
                                                                           WasAlreadyAccepted = false
                                                                         });
  }

  private OrderValidationFailure? ValidateShape(OrderAcceptanceRequest request)
  {
    if (request.Items.Count == 0)
    {
      return new() { Reason = OrderValidationFailureReason.NoItems };
    }

    if (string.IsNullOrWhiteSpace(request.TableName))
    {
      return new() { Reason = OrderValidationFailureReason.TableNameMissing };
    }

    foreach (var item in request.Items)
    {
      if (item.UnitPriceCents < 0)
      {
        return new()
               {
                 Reason = OrderValidationFailureReason.PriceOutOfRange,
                 OffendingCatalogItemId = item.CatalogItemId
               };
      }
    }

    return null;
  }

  private OrderValidationFailureReason TranslateRoutingFailureReason(RoutingFailureReason routingFailureReason)
  {
    return routingFailureReason switch
           {
             RoutingFailureReason.ItemHasNoStation => OrderValidationFailureReason.ItemHasNoStation,
             RoutingFailureReason.StationRequired => OrderValidationFailureReason.StationRequired,
             RoutingFailureReason.StationNotAssignedToItem => OrderValidationFailureReason.StationNotAssignedToItem,
             RoutingFailureReason.ChosenStationNoLongerPreparesTheItem =>
               OrderValidationFailureReason.ChosenStationNoLongerPreparesTheItem,
             _ => new Never().OfType<OrderValidationFailureReason>(routingFailureReason)
           };
  }

  private OrderValidationFailure? SettleAtAcceptance(Guid staffMemberId, BuiltOrder builtOrder)
  {
    List<SettlementLine> lines =
    [
      .. builtOrder.Items
                .Where(item => item.Settlement is not null)
                .Select(item => new SettlementLine
                                {
                                  OrderItemId = item.OrderItem.Id,
                                  PaidPriceCents = item.Settlement!.PaidPriceCents,
                                  PaymentNotice = item.Settlement!.PaymentNotice
                                })
    ];

    if (lines.Count == 0)
    {
      return null;
    }

    Result<SettlementResult, SettlementFailure> settlement =
      _settlementService.Settle(new()
                                {
                                  Lines = lines,
                                  SettledByStaffMemberId = staffMemberId
                                },
                                [.. builtOrder.Items.Select(item => new SettlementCandidate
                                                                    {
                                                                      Item = item.OrderItem,
                                                                      TableName = builtOrder.Order.TableName
                                                                    })],
                                builtOrder.Order.CreatedAtUtc);

    if (settlement.IsSuccess)
    {
      return null;
    }

    return new()
           {
             Reason = OrderValidationFailureReason.SettlementCannotBeProcessed,
             SettlementFailureReason = settlement.Failure.Reason
           };
  }

  private async Task<BuiltOrder> BuildOrderAsync(OrderAcceptanceRequest request,
                                                 Guid festivalId,
                                                 IReadOnlyCollection<ResolvedItem> resolvedItems,
                                                 CancellationToken cancellationToken)
  {
    var createdAtUtc = _clock.UtcNow;
    var globalOrderNumber =
      await _numberAllocator.AllocateGlobalOrderNumberAsync(festivalId, cancellationToken);

    Order order = new()
                  {
                    Id = Guid.NewGuid(),
                    ClientOrderId = request.ClientOrderId,
                    FestivalId = festivalId,
                    GlobalOrderNumber = globalOrderNumber,
                    StaffMemberId = request.StaffMemberId,
                    TableName = request.TableName,
                    Note = request.Note,
                    CreatedAtUtc = createdAtUtc
                  };

    Dictionary<Guid, StationOrder> stationOrdersByStationId = [];
    Dictionary<Guid, DeliveryMode> deliveryModesByStationId = request.DeliveryModes
                                                                    .GroupBy(mode => mode.StationId)
                                                                    .ToDictionary(group => group.Key, group => group.Last().DeliveryMode);

    List<BuiltOrderItem> createdItems = [];

    foreach (var resolvedItem in resolvedItems)
    {
      var resolvedStationId = resolvedItem.Decision.ResolvedStationId;

      if (!stationOrdersByStationId.TryGetValue(resolvedStationId, out var stationOrder))
      {
        var stationOrderNumber = await _numberAllocator.AllocateStationOrderNumberAsync(festivalId,
                                                                                        resolvedStationId,
                                                                                        cancellationToken);

        stationOrder = new()
                       {
                         Id = Guid.NewGuid(),
                         OrderId = order.Id,
                         FestivalId = festivalId,
                         StationId = resolvedStationId,
                         StationOrderNumber = stationOrderNumber,
                         DeliveryMode = deliveryModesByStationId.TryGetValue(resolvedStationId, out var chosenMode)
                                          ? chosenMode
                                          : DeliveryMode.Together
                       };

        stationOrdersByStationId.Add(resolvedStationId, stationOrder);
        order.StationOrders.Add(stationOrder);
      }

      OrderItem orderItem = new()
                            {
                              Id = Guid.NewGuid(),
                              StationOrderId = stationOrder.Id,
                              CatalogItemId = resolvedItem.CatalogItem.Id,
                              ItemName = resolvedItem.CatalogItem.Name,
                              UnitPriceCents = resolvedItem.Request.UnitPriceCents,
                              Note = resolvedItem.Request.Note
                            };

      stationOrder.Items.Add(orderItem);
      createdItems.Add(new()
                       {
                         OrderItem = orderItem,
                         Settlement = resolvedItem.Request.Settlement
                       });
    }

    return new()
           {
             Order = order,
             Items = createdItems
           };
  }

  private sealed record ResolvedItem
  {
    public required OrderAcceptanceItemRequest Request { get; init; }

    public required CatalogItem CatalogItem { get; init; }

    public required RoutingDecision Decision { get; init; }
  }

  private sealed record BuiltOrderItem
  {
    public required OrderItem OrderItem { get; init; }

    public required OrderSettlementLineTerms? Settlement { get; init; }
  }

  private sealed record BuiltOrder
  {
    public required Order Order { get; init; }

    public required IReadOnlyList<BuiltOrderItem> Items { get; init; }
  }
}
