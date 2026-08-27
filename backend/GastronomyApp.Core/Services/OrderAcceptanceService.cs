using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed record OrderAcceptanceLineRequest
{
    public required Guid CatalogItemId { get; init; }
    public required int Quantity { get; init; }
    public string? Note { get; init; }
    public Guid? StationId { get; init; }
}

public sealed record OrderAcceptanceRequest
{
    public required Guid ClientOrderId { get; init; }
    public required Guid StaffMemberId { get; init; }
    public required Guid DeviceId { get; init; }
    public required string TableLabel { get; init; }
    public string? Note { get; init; }
    public required IReadOnlyList<OrderAcceptanceLineRequest> Lines { get; init; }
}

public sealed class OrderAcceptanceService
{
    private const int MinimumQuantity = 1;
    private const int MaximumQuantity = 99;
    private const int MaximumTableLabelLength = 40;

    private readonly IOrderRepository _orderRepository;
    private readonly ICatalogItemRepository _catalogItemRepository;
    private readonly IStationRepository _stationRepository;
    private readonly INumberAllocator _numberAllocator;
    private readonly OrderRoutingResolver _routingResolver;
    private readonly OrderTotalCalculator _totalCalculator;
    private readonly IClock _clock;

    public OrderAcceptanceService(
        IOrderRepository orderRepository,
        ICatalogItemRepository catalogItemRepository,
        IStationRepository stationRepository,
        INumberAllocator numberAllocator,
        OrderRoutingResolver routingResolver,
        OrderTotalCalculator totalCalculator,
        IClock clock)
    {
        _orderRepository = orderRepository;
        _catalogItemRepository = catalogItemRepository;
        _stationRepository = stationRepository;
        _numberAllocator = numberAllocator;
        _routingResolver = routingResolver;
        _totalCalculator = totalCalculator;
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

        List<ResolvedLine> resolvedLines = [];
        foreach (OrderAcceptanceLineRequest lineRequest in request.Lines)
        {
            CatalogItem? catalogItem =
                await _catalogItemRepository.FindByIdAsync(lineRequest.CatalogItemId, cancellationToken);
            if (catalogItem is null)
            {
                return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(new OrderValidationFailure
                {
                    Reason = OrderValidationFailureReason.UnknownCatalogItemId,
                    OffendingCatalogItemId = lineRequest.CatalogItemId,
                });
            }

            IReadOnlyCollection<ItemStationAssignment> assignments =
                await _catalogItemRepository.FindAssignmentsAsync(lineRequest.CatalogItemId, cancellationToken);

            Result<RoutingDecision, RoutingFailure> routing = _routingResolver.Resolve(
                lineRequest.CatalogItemId,
                assignments,
                activeStations,
                lineRequest.StationId);

            if (!routing.IsSuccess)
            {
                return Result<OrderAcceptanceResult, OrderValidationFailure>.Failed(new OrderValidationFailure
                {
                    Reason = ReasonFor(routing.Failure.Reason),
                    OffendingCatalogItemId = lineRequest.CatalogItemId,
                });
            }

            resolvedLines.Add(new ResolvedLine
            {
                Request = lineRequest,
                CatalogItem = catalogItem,
                Decision = routing.Value,
            });
        }

        Order order = await BuildOrderAsync(request, resolvedLines, cancellationToken);
        await _orderRepository.AddAsync(order, cancellationToken);

        return Result<OrderAcceptanceResult, OrderValidationFailure>.Success(new OrderAcceptanceResult
        {
            Order = order,
            WasAlreadyAccepted = false,
        });
    }

    private sealed record ResolvedLine
    {
        public required OrderAcceptanceLineRequest Request { get; init; }
        public required CatalogItem CatalogItem { get; init; }
        public required RoutingDecision Decision { get; init; }
    }

    private OrderValidationFailure? ValidateShape(OrderAcceptanceRequest request)
    {
        if (request.Lines.Count == 0)
        {
            return new OrderValidationFailure { Reason = OrderValidationFailureReason.NoLines };
        }

        if (string.IsNullOrWhiteSpace(request.TableLabel))
        {
            return new OrderValidationFailure { Reason = OrderValidationFailureReason.TableLabelMissing };
        }

        if (request.TableLabel.Length > MaximumTableLabelLength)
        {
            return new OrderValidationFailure { Reason = OrderValidationFailureReason.TableLabelTooLong };
        }

        foreach (OrderAcceptanceLineRequest line in request.Lines)
        {
            if (line.Quantity < MinimumQuantity || line.Quantity > MaximumQuantity)
            {
                return new OrderValidationFailure
                {
                    Reason = OrderValidationFailureReason.QuantityOutOfRange,
                    OffendingCatalogItemId = line.CatalogItemId,
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
        IReadOnlyCollection<ResolvedLine> resolvedLines,
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
            DeviceId = request.DeviceId,
            TableLabel = request.TableLabel,
            Note = request.Note,
            TotalCents = 0,
            Status = OrderStatus.Accepted,
            CreatedAtUtc = createdAtUtc,
        };

        Dictionary<Guid, LocationTicket> ticketsByStationId = [];

        foreach (ResolvedLine resolvedLine in resolvedLines)
        {
            Guid resolvedStationId = resolvedLine.Decision.ResolvedStationId;

            if (!ticketsByStationId.TryGetValue(resolvedStationId, out LocationTicket? ticket))
            {
                int stationSequenceNumber = await _numberAllocator.AllocateStationSequenceNumberAsync(
                    resolvedStationId,
                    cancellationToken);

                ticket = new LocationTicket
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    StationId = resolvedStationId,
                    StationSequenceNumber = stationSequenceNumber,
                    Status = LocationTicketStatus.Queued,
                    ReprintCount = 0,
                    CreatedAtUtc = createdAtUtc,
                };

                ticketsByStationId.Add(resolvedStationId, ticket);
                order.Tickets.Add(ticket);
            }

            order.Lines.Add(new OrderLine
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                LocationTicketId = ticket.Id,
                CatalogItemId = resolvedLine.CatalogItem.Id,
                ChosenStationId = resolvedLine.Decision.ChosenStationId,
                ItemNameSnapshot = resolvedLine.CatalogItem.Name,
                UnitPriceCentsSnapshot = resolvedLine.CatalogItem.PriceCents,
                Quantity = resolvedLine.Request.Quantity,
                Note = resolvedLine.Request.Note,
            });
        }

        order.TotalCents = _totalCalculator.CalculateTotalCents(order.Lines);

        return order;
    }
}
