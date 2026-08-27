using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public sealed record LoadedOrder(
    Order Order,
    IReadOnlyList<OrderLine> Lines,
    IReadOnlyList<LocationTicket> Tickets,
    IReadOnlyDictionary<Guid, string> LocationNames);

public sealed class OrderReader
{
    public async Task<LoadedOrder?> LoadAsync(
        GastronomyAppDbContext context,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        Order? order = await context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

        return order is null ? null : await LoadForAsync(context, order, cancellationToken);
    }

    public async Task<LoadedOrder?> LoadByClientOrderIdAsync(
        GastronomyAppDbContext context,
        Guid clientOrderId,
        CancellationToken cancellationToken)
    {
        Order? order = await context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.ClientOrderId == clientOrderId, cancellationToken);

        return order is null ? null : await LoadForAsync(context, order, cancellationToken);
    }

    public PlacedOrderView Describe(LoadedOrder loaded, int expectedTotalCents)
    {
        return new PlacedOrderView(
            loaded.Order.Id,
            loaded.Order.GlobalOrderNumber,
            loaded.Order.Status.ToString(),
            loaded.Order.TotalCents,
            expectedTotalCents,
            loaded.Order.CreatedAtUtc,
            DescribeTickets(loaded));
    }

    public IReadOnlyList<OrderTicketView> DescribeTickets(LoadedOrder loaded)
    {
        return
        [
            .. loaded.Tickets.Select(ticket => new OrderTicketView(
                ticket.Id,
                ticket.ProductionLocationId,
                loaded.LocationNames.TryGetValue(ticket.ProductionLocationId, out string? name) ? name : string.Empty,
                ticket.LocationSequenceNumber,
                ticket.Status.ToString(),
                [
                    .. loaded.Lines
                        .Where(line => line.LocationTicketId == ticket.Id)
                        .Select(line => line.Id),
                ])),
        ];
    }

    private async Task<LoadedOrder> LoadForAsync(
        GastronomyAppDbContext context,
        Order order,
        CancellationToken cancellationToken)
    {
        List<OrderLine> lines = await context.OrderLines
            .AsNoTracking()
            .Where(line => line.OrderId == order.Id)
            .OrderBy(line => line.Id)
            .ToListAsync(cancellationToken);

        List<LocationTicket> tickets = await context.LocationTickets
            .AsNoTracking()
            .Where(ticket => ticket.OrderId == order.Id)
            .OrderBy(ticket => ticket.LocationSequenceNumber)
            .ThenBy(ticket => ticket.Id)
            .ToListAsync(cancellationToken);

        HashSet<Guid> locationIds = [.. tickets.Select(ticket => ticket.ProductionLocationId)];

        Dictionary<Guid, string> locationNames = await context.ProductionLocations
            .AsNoTracking()
            .Where(location => locationIds.Contains(location.Id))
            .ToDictionaryAsync(location => location.Id, location => location.Name, cancellationToken);

        return new LoadedOrder(order, lines, tickets, locationNames);
    }
}

public sealed class OrderStatusProjectionWriter
{
    private readonly OrderStatusCalculator statusCalculator;
    private readonly ImmediateTransactionRunner transactionRunner = new();

    public OrderStatusProjectionWriter(OrderStatusCalculator statusCalculator)
    {
        this.statusCalculator = statusCalculator;
    }

    public Task<OrderStatus> WriteAsync(
        GastronomyAppDbContext context,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        return transactionRunner.RunAsync(
            context,
            async transactionCancellationToken =>
            {
                OrderStatus status = await ApplyAsync(context, orderId, transactionCancellationToken);

                return new TransactionOutcome<OrderStatus> { Value = status, ShouldCommit = true };
            },
            cancellationToken);
    }

    public async Task<OrderStatus> ApplyAsync(
        GastronomyAppDbContext context,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        Order order = await context.Orders.FirstAsync(candidate => candidate.Id == orderId, cancellationToken);

        EventSession session = await context.EventSessions
            .FirstAsync(candidate => candidate.Id == order.EventSessionId, cancellationToken);

        List<LocationTicketStatus> ticketStatuses = await context.LocationTickets
            .Where(ticket => ticket.OrderId == orderId)
            .Select(ticket => ticket.Status)
            .ToListAsync(cancellationToken);

        order.Status = statusCalculator.Calculate(ticketStatuses, session.IsPractice);
        await context.SaveChangesAsync(cancellationToken);

        return order.Status;
    }
}
