using GastronomyApp.Contracts;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Exceptions;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
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

  public async Task<Result<OrderAcceptanceResult, OrderValidationFailure>> AcceptAsync(PlaceOrderRequest request, Guid staffMemberId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    try
    {
      return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                               {
                                                 Result<OrderAcceptanceResult, OrderValidationFailure> acceptance = await AcceptInsideTransactionAsync(request, staffMemberId, transactionCancellationToken);

                                                 return new TransactionOutcome<Result<OrderAcceptanceResult, OrderValidationFailure>>
                                                        {
                                                          Value = acceptance,
                                                          ShouldCommit = acceptance.IsSuccess
                                                        };
                                               },
                                               cancellationToken);
    }
    catch (ConcurrentWriteException)
    {
      return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(new() { Reason = OrderValidationFailureReason.OrderNumberCouldNotBeAllocated });
    }
  }

  private async Task<Result<OrderAcceptanceResult, OrderValidationFailure>> AcceptInsideTransactionAsync(PlaceOrderRequest request, Guid staffMemberId, CancellationToken cancellationToken)
  {
    var shapeFailure = ValidateShape(request);
    if (shapeFailure is not null)
      return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(shapeFailure);

    var existingOrder = await _orderRepository.FindByClientOrderIdAsync(request.ClientOrderId, cancellationToken);
    if (existingOrder is not null)
    {
      return Result<OrderAcceptanceResult, OrderValidationFailure>.Success(new()
                                                                           {
                                                                             Order = existingOrder,
                                                                             WasAlreadyAccepted = true
                                                                           });
    }

    var festival = await _runningFestival.FindAsync(cancellationToken);
    if (festival is null)
      return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(new() { Reason = OrderValidationFailureReason.NoRunningFestival });

    Result<IReadOnlyList<ResolvedOrderItem>, OrderValidationFailure> resolution = await _itemResolutionService.ResolveAsync(festival.Id, request.Items ?? [], cancellationToken);

    if (!resolution.IsSuccess)
      return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(resolution.Failure);

    IReadOnlyList<ResolvedOrderItem> resolvedItems = resolution.Value;

    var builtOrder = await BuildOrderAsync(request, staffMemberId, festival.Id, resolvedItems, cancellationToken);

    var settlementFailure = SettleAtAcceptance(staffMemberId, builtOrder);
    if (settlementFailure is not null)
      return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(settlementFailure);

    await _orderRepository.AddAsync(builtOrder.Order, cancellationToken);

    return Result<OrderAcceptanceResult, OrderValidationFailure>.Success(new()
                                                                         {
                                                                           Order = builtOrder.Order,
                                                                           WasAlreadyAccepted = false
                                                                         });
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

  private OrderValidationFailure? SettleAtAcceptance(Guid staffMemberId, BuiltOrder builtOrder)
  {
    List<SettleLineRequest> lines = builtOrder.Items.Where(item => item.Settlement is not null)
                                              .Select(item => new SettleLineRequest
                                                              {
                                                                OrderItemId = item.OrderItem.Id,
                                                                PaidPriceCents = item.Settlement!.PaidPriceCents,
                                                                PaymentNotice = item.Settlement!.PaymentNotice
                                                              })
                                              .ToList();

    if (lines.Count == 0)
      return null;

    Result<SettlementResult, SettlementFailure> settlement = _settlementService.Settle(lines,
                                                                                       staffMemberId,
                                                                                       builtOrder.Items.Select(item => new SettlementCandidate
                                                                                                                       {
                                                                                                                         Item = item.OrderItem,
                                                                                                                         TableName = builtOrder.Order.TableName
                                                                                                                       })
                                                                                                 .ToList(),
                                                                                       builtOrder.Order.CreatedAtUtc);

    if (settlement.IsSuccess)
      return null;

    return new()
           {
             Reason = OrderValidationFailureReason.SettlementCannotBeProcessed,
             SettlementFailureReason = settlement.Failure.Reason
           };
  }

  private async Task<BuiltOrder> BuildOrderAsync(PlaceOrderRequest request, Guid staffMemberId, Guid festivalId, IReadOnlyCollection<ResolvedOrderItem> resolvedItems, CancellationToken cancellationToken)
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

    Dictionary<Guid, StationOrder> stationOrdersByStationId = [];
    Dictionary<Guid, DeliveryMode> deliveryModesByStationId = (request.DeliveryModes ?? []).GroupBy(mode => mode.StationId).ToDictionary(group => group.Key, group => group.Last().DeliveryMode);

    List<BuiltOrderItem> createdItems = [];

    foreach (var resolvedItem in resolvedItems)
    {
      var resolvedStationId = resolvedItem.Decision.ResolvedStationId;

      if (!stationOrdersByStationId.TryGetValue(resolvedStationId, out var stationOrder))
      {
        var stationOrderNumber = await _numberAllocator.AllocateStationOrderNumberAsync(festivalId, resolvedStationId, cancellationToken);

        stationOrder = new()
                       {
                         Id = Guid.NewGuid(),
                         OrderId = order.Id,
                         FestivalId = festivalId,
                         StationId = resolvedStationId,
                         StationOrderNumber = stationOrderNumber,
                         DeliveryMode = DeliveryModeFor(deliveryModesByStationId, resolvedStationId)
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

  private DeliveryMode DeliveryModeFor(IReadOnlyDictionary<Guid, DeliveryMode> deliveryModesByStationId, Guid stationId)
  {
    if (deliveryModesByStationId.TryGetValue(stationId, out var chosenMode))
      return chosenMode;

    return DeliveryMode.Together;
  }

  private sealed record BuiltOrderItem
  {
    public required OrderItem OrderItem { get; init; }

    public required OrderSettlementLineRequest? Settlement { get; init; }
  }

  private sealed record BuiltOrder
  {
    public required Order Order { get; init; }

    public required IReadOnlyList<BuiltOrderItem> Items { get; init; }
  }
}
