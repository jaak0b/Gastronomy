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

    public async Task<IResult> ListForStaffMemberAsync(Guid staffMemberId, CancellationToken cancellationToken)
    {
        List<Order> orders = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.StaffMemberId == staffMemberId)
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

    public async Task<IResult> DetailAsync(Guid orderId, Guid staffMemberId, CancellationToken cancellationToken)
    {
        LoadedOrder? loaded = await orderReader.LoadAsync(dbContext, orderId, cancellationToken);

        if (loaded is null || loaded.Order.StaffMemberId != staffMemberId)
        {
            return Results.NotFound();
        }

        return Results.Ok(await DescribeDetailAsync(loaded, cancellationToken));
    }

    public async Task<OrderDetailView> DescribeDetailAsync(LoadedOrder loaded, CancellationToken cancellationToken)
    {
        Dictionary<Guid, Guid> ticketStations = loaded.Tickets
            .ToDictionary(ticket => ticket.Id, ticket => ticket.StationId);

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
                StationNameFor(loaded, ticketStations, line.LocationTicketId))),
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
        HashSet<Guid> stationIds = [.. loaded.Tickets.Select(ticket => ticket.StationId)];

        Dictionary<Guid, PrinterStatus> printerStatuses = await dbContext.PrinterStatuses
            .AsNoTracking()
            .Where(status => stationIds.Contains(status.StationId))
            .ToDictionaryAsync(status => status.StationId, cancellationToken);

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
                loaded.StationNames.TryGetValue(ticket.StationId, out string? name) ? name : string.Empty,
                ticket.StationSequenceNumber,
                ticket.Status.ToString(),
                latestJobs.TryGetValue(ticket.Id, out PrintJob? job) ? job.FailureReason?.ToString() : null,
                !printerStatuses.TryGetValue(ticket.StationId, out PrinterStatus? status)
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

    private string StationNameFor(
        LoadedOrder loaded,
        IReadOnlyDictionary<Guid, Guid> ticketStations,
        Guid locationTicketId)
    {
        if (!ticketStations.TryGetValue(locationTicketId, out Guid stationId))
        {
            return string.Empty;
        }

        return loaded.StationNames.TryGetValue(stationId, out string? name) ? name : string.Empty;
    }
}
