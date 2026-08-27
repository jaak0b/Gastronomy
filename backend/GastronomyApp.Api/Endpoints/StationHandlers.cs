using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public sealed class StationQueryHandler
{
    private readonly GastronomyAppDbContext dbContext;
    private readonly StationCallerAccessor callerAccessor;
    private readonly StationPrintabilityReader printabilityReader;
    private readonly StationTicketDescriber ticketDescriber;
    private readonly PrinterStatusReader printerStatusReader;

    public StationQueryHandler(
        GastronomyAppDbContext dbContext,
        StationCallerAccessor callerAccessor,
        StationPrintabilityReader printabilityReader,
        StationTicketDescriber ticketDescriber,
        PrinterStatusReader printerStatusReader)
    {
        this.dbContext = dbContext;
        this.callerAccessor = callerAccessor;
        this.printabilityReader = printabilityReader;
        this.ticketDescriber = ticketDescriber;
        this.printerStatusReader = printerStatusReader;
    }

    public async Task<IResult> ListLocationsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, StationPrintability> printability =
            await printabilityReader.ReadAsync(dbContext, cancellationToken);

        List<ProductionLocation> locations = await dbContext.ProductionLocations
            .AsNoTracking()
            .Where(location => location.IsActive)
            .OrderBy(location => location.SortOrder)
            .ToListAsync(cancellationToken);

        List<StationLocationView> views =
        [
            .. locations.Select(location => new StationLocationView(
                location.Id,
                location.Name,
                location.SortOrder,
                printability.TryGetValue(location.Id, out StationPrintability? station)
                    && printabilityReader.CanPrintRightNow(station))),
        ];

        return Results.Ok(new StationLocationListView(views));
    }

    public async Task<IResult> ListTicketsAsync(
        HttpContext httpContext,
        Guid? locationId,
        CancellationToken cancellationToken)
    {
        Guid selectedLocationId = locationId ?? callerAccessor.Read(httpContext).ProductionLocationId;

        return Results.Ok(new StationTicketListView(
            selectedLocationId,
            await ticketDescriber.DescribeOpenTicketsAsync(dbContext, selectedLocationId, cancellationToken)));
    }

    public async Task<IResult> StatusAsync(
        HttpContext httpContext,
        Guid? locationId,
        CancellationToken cancellationToken)
    {
        Guid selectedLocationId = locationId ?? callerAccessor.Read(httpContext).ProductionLocationId;

        PrinterStatusListView all = await printerStatusReader.ReadAsync(dbContext, cancellationToken);
        PrinterStatusView? selected = all.Locations.FirstOrDefault(view => view.LocationId == selectedLocationId);

        return selected is null ? Results.NotFound() : Results.Ok(selected);
    }
}

public sealed class StationTicketDescriber
{
    private readonly TicketAcknowledgePolicy acknowledgePolicy;
    private readonly StationPrintabilityReader printabilityReader;

    public StationTicketDescriber(
        TicketAcknowledgePolicy acknowledgePolicy,
        StationPrintabilityReader printabilityReader)
    {
        this.acknowledgePolicy = acknowledgePolicy;
        this.printabilityReader = printabilityReader;
    }

    public async Task<IReadOnlyList<StationTicketView>> DescribeOpenTicketsAsync(
        GastronomyAppDbContext dbContext,
        Guid locationId,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, StationPrintability> printability =
            await printabilityReader.ReadAsync(dbContext, cancellationToken);

        ProductionLocation? location = await dbContext.ProductionLocations
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == locationId, cancellationToken);

        List<LocationTicket> tickets = await dbContext.LocationTickets
            .AsNoTracking()
            .Where(ticket => ticket.ProductionLocationId == locationId
                && ticket.Status != LocationTicketStatus.Printed
                && ticket.Status != LocationTicketStatus.PrintedOnTestPrinter
                && ticket.Status != LocationTicketStatus.HandledOnPaper)
            .OrderBy(ticket => ticket.LocationSequenceNumber)
            .ToListAsync(cancellationToken);

        HashSet<Guid> orderIds = [.. tickets.Select(ticket => ticket.OrderId)];
        HashSet<Guid> ticketIds = [.. tickets.Select(ticket => ticket.Id)];

        Dictionary<Guid, Order> orders = await dbContext.Orders
            .AsNoTracking()
            .Where(order => orderIds.Contains(order.Id))
            .ToDictionaryAsync(order => order.Id, cancellationToken);

        List<OrderLine> lines = await dbContext.OrderLines
            .AsNoTracking()
            .Where(line => ticketIds.Contains(line.LocationTicketId))
            .OrderBy(line => line.Id)
            .ToListAsync(cancellationToken);

        List<StationTicketView> views = [];

        foreach (LocationTicket ticket in tickets)
        {
            if (!orders.TryGetValue(ticket.OrderId, out Order? order))
            {
                continue;
            }

            printability.TryGetValue(locationId, out StationPrintability? station);
            StationPrintability resolved = station ?? UnknownStation();
            bool canAcknowledge = acknowledgePolicy.CanAcknowledge(ticket.Status, resolved);

            views.Add(new StationTicketView(
                ticket.Id,
                order.Id,
                locationId,
                location?.Name ?? string.Empty,
                ticket.LocationSequenceNumber,
                order.GlobalOrderNumber,
                order.TableLabel,
                order.Note,
                order.CreatedAtUtc,
                ticket.Status.ToString(),
                ticket.ReprintCount,
                canAcknowledge,
                canAcknowledge ? null : RefusalKeyFor(ticket.Status),
                [
                    .. lines
                        .Where(line => line.LocationTicketId == ticket.Id)
                        .Select(line => new StationTicketLineView(line.Quantity, line.ItemNameSnapshot, line.Note)),
                ]));
        }

        return views;
    }

    public string RefusalKeyFor(LocationTicketStatus status)
    {
        return status == LocationTicketStatus.HandledOnPaper ? "station.alreadyTaken" : "station.takeRefused";
    }

    private StationPrintability UnknownStation()
    {
        return new StationPrintability
        {
            IsFaulty = true,
            IsOnline = false,
            IsPaperEnd = false,
            IsCoverOpen = false,
            IsInErrorState = false,
            IsEnabled = false,
        };
    }
}

public sealed class StationAcknowledgeHandler
{
    private readonly GastronomyAppDbContext dbContext;
    private readonly TicketAcknowledgePolicy acknowledgePolicy;
    private readonly TicketStateMachine stateMachine;
    private readonly StationPrintabilityReader printabilityReader;
    private readonly StationTicketDescriber ticketDescriber;
    private readonly OrderStatusProjectionWriter projectionWriter;
    private readonly HubNotificationDispatcher dispatcher;
    private readonly ResultEnvelope resultEnvelope;
    private readonly ImmediateTransactionRunner transactionRunner = new();

    public StationAcknowledgeHandler(
        GastronomyAppDbContext dbContext,
        TicketAcknowledgePolicy acknowledgePolicy,
        TicketStateMachine stateMachine,
        StationPrintabilityReader printabilityReader,
        StationTicketDescriber ticketDescriber,
        OrderStatusProjectionWriter projectionWriter,
        HubNotificationDispatcher dispatcher,
        ResultEnvelope resultEnvelope)
    {
        this.dbContext = dbContext;
        this.acknowledgePolicy = acknowledgePolicy;
        this.stateMachine = stateMachine;
        this.printabilityReader = printabilityReader;
        this.ticketDescriber = ticketDescriber;
        this.projectionWriter = projectionWriter;
        this.dispatcher = dispatcher;
        this.resultEnvelope = resultEnvelope;
    }

    public async Task<IResult> AcknowledgeAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        LocationTicket? ticket = await dbContext.LocationTickets
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == ticketId, cancellationToken);

        if (ticket is null)
        {
            return Results.NotFound();
        }

        IReadOnlyDictionary<Guid, StationPrintability> printability =
            await printabilityReader.ReadAsync(dbContext, cancellationToken);

        if (!printability.TryGetValue(ticket.ProductionLocationId, out StationPrintability? station))
        {
            return Results.NotFound();
        }

        if (ticket.Status is LocationTicketStatus.HandledOnPaper)
        {
            return resultEnvelope.Problem(
                StatusCodes.Status409Conflict,
                "AcknowledgeRefused",
                "station.alreadyTaken");
        }

        if (!acknowledgePolicy.CanAcknowledge(ticket.Status, station))
        {
            return resultEnvelope.Problem(
                StatusCodes.Status409Conflict,
                "AcknowledgeRefused",
                ticketDescriber.RefusalKeyFor(ticket.Status));
        }

        if (!stateMachine.CanTransition(ticket.Status, LocationTicketStatus.HandledOnPaper))
        {
            return resultEnvelope.Problem(
                StatusCodes.Status409Conflict,
                "IllegalTicketTransition",
                "ticket.illegalTransition");
        }

        Guid orderId = ticket.OrderId;

        OrderStatus orderStatus = await transactionRunner.RunAsync(
            dbContext,
            async transactionCancellationToken =>
            {
                LocationTicket tracked = await dbContext.LocationTickets
                    .FirstAsync(candidate => candidate.Id == ticketId, transactionCancellationToken);
                tracked.Status = LocationTicketStatus.HandledOnPaper;
                await dbContext.SaveChangesAsync(transactionCancellationToken);

                OrderStatus status = await projectionWriter.ApplyAsync(
                    dbContext,
                    orderId,
                    transactionCancellationToken);

                return new TransactionOutcome<OrderStatus> { Value = status, ShouldCommit = true };
            },
            cancellationToken);

        await dispatcher.OnTicketStatusChangedAsync(
            orderId,
            ticketId,
            LocationTicketStatus.HandledOnPaper,
            null,
            cancellationToken);

        await dispatcher.OnOrderStatusChangedAsync(orderId, orderStatus, cancellationToken);

        return Results.Ok(new AcknowledgedTicketView(ticketId, LocationTicketStatus.HandledOnPaper.ToString()));
    }
}

public sealed record AcknowledgedTicketView(Guid TicketId, string Status);
