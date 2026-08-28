using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed record OrderAcceptanceItemRequest
{
    public required Guid CatalogItemId { get; init; }
    public required int UnitPriceCents { get; init; }
    public string? Note { get; init; }
    public Guid? StationId { get; init; }
}

public sealed record OrderAcceptanceRequest
{
    public required Guid ClientOrderId { get; init; }
    public required Guid StaffMemberId { get; init; }
    public required string TableName { get; init; }
    public string? Note { get; init; }
    public required IReadOnlyList<OrderAcceptanceItemRequest> Items { get; init; }
}

public sealed class OrderAcceptanceService
{
    private const int MaximumItems = 200;
    private const int MaximumTableNameLength = 40;

    private readonly IOrderRepository _orderRepository;
    private readonly ICatalogItemRepository _catalogItemRepository;
    private readonly IStationRepository _stationRepository;
    private readonly INumberAllocator _numberAllocator;
    private readonly OrderRoutingResolver _routingResolver;
    private readonly IClock _clock;

    public OrderAcceptanceService(
        IOrderRepository orderRepository,
        ICatalogItemRepository catalogItemRepository,
        IStationRepository stationRepository,
        INumberAllocator numberAllocator,
        OrderRoutingResolver routingResolver,
        IClock clock)
    {
        _orderRepository = orderRepository;
        _catalogItemRepository = catalogItemRepository;
        _stationRepository = stationRepository;
        _numberAllocator = numberAllocator;
        _routingResolver = routingResolver;
        _clock = clock;
    }

    public async Task<Result<OrderAcceptanceResult, OrderValidationFailure>> AcceptAsync(
        OrderAcceptanceRequest request,
        CancellationToken cancellationToken)
    {
        OrderValidationFailure? shapeFailure = ValidateShape(request);
        if (shapeFailure is not null)
        {
            return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(shapeFailure);
        }

        Order? existingOrder =
            await _orderRepository.FindByClientOrderIdAsync(request.ClientOrderId, cancellationToken);
        if (existingOrder is not null)
        {
            return Result<OrderAcceptanceResult, OrderValidationFailure>.Success(new OrderAcceptanceResult
            {
                Order = existingOrder,
                WasAlreadyAccepted = true,
            });
        }

        IReadOnlyCollection<Station> activeStations =
            await _stationRepository.FindActiveAsync(cancellationToken);

        List<ResolvedItem> resolvedItems = [];
        foreach (OrderAcceptanceItemRequest itemRequest in request.Items)
        {
            CatalogItem? catalogItem =
                await _catalogItemRepository.FindByIdAsync(itemRequest.CatalogItemId, cancellationToken);
            if (catalogItem is null)
            {
                return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(new OrderValidationFailure
                {
                    Reason = OrderValidationFailureReason.UnknownCatalogItemId,
                    OffendingCatalogItemId = itemRequest.CatalogItemId,
                });
            }

            IReadOnlyCollection<ItemStationAssignment> assignments =
                await _catalogItemRepository.FindAssignmentsAsync(itemRequest.CatalogItemId, cancellationToken);

            Result<RoutingDecision, RoutingFailure> routing = _routingResolver.Resolve(
                itemRequest.CatalogItemId,
                assignments,
                activeStations,
                itemRequest.StationId);

            if (!routing.IsSuccess)
            {
                return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(new OrderValidationFailure
                {
                    Reason = ReasonFor(routing.Failure.Reason),
                    OffendingCatalogItemId = itemRequest.CatalogItemId,
                });
            }

            resolvedItems.Add(new ResolvedItem
            {
                Request = itemRequest,
                CatalogItem = catalogItem,
                Decision = routing.Value,
            });
        }

        Order order = await BuildOrderAsync(request, resolvedItems, cancellationToken);
        await _orderRepository.AddAsync(order, cancellationToken);

        return Result<OrderAcceptanceResult, OrderValidationFailure>.Success(new OrderAcceptanceResult
        {
            Order = order,
            WasAlreadyAccepted = false,
        });
    }

    private sealed record ResolvedItem
    {
        public required OrderAcceptanceItemRequest Request { get; init; }
        public required CatalogItem CatalogItem { get; init; }
        public required RoutingDecision Decision { get; init; }
    }

    private OrderValidationFailure? ValidateShape(OrderAcceptanceRequest request)
    {
        if (request.Items.Count == 0)
        {
            return new OrderValidationFailure { Reason = OrderValidationFailureReason.NoItems };
        }

        if (request.Items.Count > MaximumItems)
        {
            return new OrderValidationFailure { Reason = OrderValidationFailureReason.TooManyItems };
        }

        if (string.IsNullOrWhiteSpace(request.TableName))
        {
            return new OrderValidationFailure { Reason = OrderValidationFailureReason.TableNameMissing };
        }

        if (request.TableName.Length > MaximumTableNameLength)
        {
            return new OrderValidationFailure { Reason = OrderValidationFailureReason.TableNameTooLong };
        }

        foreach (OrderAcceptanceItemRequest item in request.Items)
        {
            if (item.UnitPriceCents < 0)
            {
                return new OrderValidationFailure
                {
                    Reason = OrderValidationFailureReason.PriceOutOfRange,
                    OffendingCatalogItemId = item.CatalogItemId,
                };
            }
        }

        return null;
    }

    private OrderValidationFailureReason ReasonFor(RoutingFailureReason routingFailureReason)
    {
        return routingFailureReason switch
        {
            RoutingFailureReason.ItemHasNoStation => OrderValidationFailureReason.ItemHasNoStation,
            RoutingFailureReason.StationRequired => OrderValidationFailureReason.StationRequired,
            RoutingFailureReason.StationNotAssignedToItem => OrderValidationFailureReason.StationNotAssignedToItem,
            _ => new Never().OfType<OrderValidationFailureReason>(routingFailureReason),
        };
    }

    private async Task<Order> BuildOrderAsync(
        OrderAcceptanceRequest request,
        IReadOnlyCollection<ResolvedItem> resolvedItems,
        CancellationToken cancellationToken)
    {
        DateTime createdAtUtc = _clock.UtcNow;
        int globalOrderNumber =
            await _numberAllocator.AllocateGlobalOrderNumberAsync(cancellationToken);

        Order order = new()
        {
            Id = Guid.NewGuid(),
            ClientOrderId = request.ClientOrderId,
            GlobalOrderNumber = globalOrderNumber,
            StaffMemberId = request.StaffMemberId,
            TableName = request.TableName,
            Note = request.Note,
            CreatedAtUtc = createdAtUtc,
        };

        Dictionary<Guid, StationOrder> stationOrdersByStationId = [];

        foreach (ResolvedItem resolvedItem in resolvedItems)
        {
            Guid resolvedStationId = resolvedItem.Decision.ResolvedStationId;

            if (!stationOrdersByStationId.TryGetValue(resolvedStationId, out StationOrder? stationOrder))
            {
                int stationOrderNumber = await _numberAllocator.AllocateStationOrderNumberAsync(
                    resolvedStationId,
                    cancellationToken);

                stationOrder = new StationOrder
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    StationId = resolvedStationId,
                    StationOrderNumber = stationOrderNumber,
                };

                stationOrder.PrintJobs.Add(new PrintJob
                {
                    Id = Guid.NewGuid(),
                    StationOrderId = stationOrder.Id,
                    CopyNumber = 0,
                    Status = PrintJobStatus.Queued,
                    CreatedAtUtc = createdAtUtc,
                });

                stationOrdersByStationId.Add(resolvedStationId, stationOrder);
                order.StationOrders.Add(stationOrder);
            }

            stationOrder.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                StationOrderId = stationOrder.Id,
                CatalogItemId = resolvedItem.CatalogItem.Id,
                ItemName = resolvedItem.CatalogItem.Name,
                UnitPriceCents = resolvedItem.Request.UnitPriceCents,
                Note = resolvedItem.Request.Note,
            });
        }

        return order;
    }
}
