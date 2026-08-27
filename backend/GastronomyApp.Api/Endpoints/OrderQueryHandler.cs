using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public sealed class OrderQueryHandler
{
    private const int DefaultLimit = 50;

    private readonly GastronomyAppDbContext dbContext;
    private readonly OrderReader orderReader;

    public OrderQueryHandler(GastronomyAppDbContext dbContext, OrderReader orderReader)
    {
        this.dbContext = dbContext;
        this.orderReader = orderReader;
    }

    public async Task<IResult> ListForPersonAsync(Guid serverPersonId, CancellationToken cancellationToken)
    {
        List<Order> orders = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.ServerPersonId == serverPersonId)
            .OrderByDescending(order => order.CreatedAtUtc)
            .Take(DefaultLimit)
            .ToListAsync(cancellationToken);

        List<OrderListEntryView> entries = [];

        foreach (Order order in orders)
        {
            LoadedOrder loaded = (await orderReader.LoadAsync(dbContext, order.Id, cancellationToken))!;
            entries.Add(await DescribeListEntryAsync(loaded, cancellationToken));
        }

        return Results.Ok(new OrderListView(entries));
    }

    public async Task<IResult> DetailAsync(Guid orderId, Guid serverPersonId, CancellationToken cancellationToken)
    {
        LoadedOrder? loaded = await orderReader.LoadAsync(dbContext, orderId, cancellationToken);

        if (loaded is null || loaded.Order.ServerPersonId != serverPersonId)
        {
            return Results.NotFound();
        }

        return Results.Ok(await DescribeDetailAsync(loaded, cancellationToken));
    }

    public async Task<OrderDetailView> DescribeDetailAsync(LoadedOrder loaded, CancellationToken cancellationToken)
    {
        Dictionary<Guid, Guid> ticketLocations = loaded.Tickets
            .ToDictionary(ticket => ticket.Id, ticket => ticket.ProductionLocationId);

        await Task.CompletedTask;

        List<OrderDetailLineView> lines =
        [
            .. loaded.Lines.Select(line => new OrderDetailLineView(
                line.Id,
                line.CatalogItemId,
                line.ItemNameSnapshot,
                line.Quantity,
                line.UnitPriceCentsSnapshot,
                line.Note,
                LocationNameFor(loaded, ticketLocations, line.LocationTicketId))),
        ];

        return new OrderDetailView(
            loaded.Order.Id,
            loaded.Order.GlobalOrderNumber,
            loaded.Order.TableLabel,
            loaded.Order.Note,
            loaded.Order.TotalCents,
            loaded.Order.Status.ToString(),
            loaded.Order.CreatedAtUtc,
            lines,
            orderReader.DescribeTickets(loaded));
    }

    public async Task<OrderListEntryView> DescribeListEntryAsync(
        LoadedOrder loaded,
        CancellationToken cancellationToken)
    {
        HashSet<Guid> locationIds = [.. loaded.Tickets.Select(ticket => ticket.ProductionLocationId)];

        Dictionary<Guid, PrinterStatus> printerStatuses = await dbContext.PrinterStatuses
            .AsNoTracking()
            .Where(status => locationIds.Contains(status.ProductionLocationId))
            .ToDictionaryAsync(status => status.ProductionLocationId, cancellationToken);

        HashSet<Guid> ticketIds = [.. loaded.Tickets.Select(ticket => ticket.Id)];

        Dictionary<Guid, PrintJob> latestJobs = await dbContext.PrintJobs
            .AsNoTracking()
            .Where(job => job.LocationTicketId != null && ticketIds.Contains(job.LocationTicketId!.Value))
            .GroupBy(job => job.LocationTicketId!.Value)
            .Select(group => group.OrderByDescending(job => job.CreatedAtUtc).First())
            .ToDictionaryAsync(job => job.LocationTicketId!.Value, cancellationToken);

        List<OrderListTicketView> tickets =
        [
            .. loaded.Tickets.Select(ticket => new OrderListTicketView(
                ticket.Id,
                loaded.LocationNames.TryGetValue(ticket.ProductionLocationId, out string? name) ? name : string.Empty,
                ticket.LocationSequenceNumber,
                ticket.Status.ToString(),
                latestJobs.TryGetValue(ticket.Id, out PrintJob? job) ? job.FailureReason?.ToString() : null,
                !printerStatuses.TryGetValue(ticket.ProductionLocationId, out PrinterStatus? status)
                    || !status.IsPaperEnd)),
        ];

        return new OrderListEntryView(
            loaded.Order.Id,
            loaded.Order.GlobalOrderNumber,
            loaded.Order.TableLabel,
            loaded.Order.TotalCents,
            loaded.Order.Status.ToString(),
            loaded.Order.CreatedAtUtc,
            tickets);
    }

    private string LocationNameFor(
        LoadedOrder loaded,
        IReadOnlyDictionary<Guid, Guid> ticketLocations,
        Guid locationTicketId)
    {
        if (!ticketLocations.TryGetValue(locationTicketId, out Guid locationId))
        {
            return string.Empty;
        }

        return loaded.LocationNames.TryGetValue(locationId, out string? name) ? name : string.Empty;
    }
}
