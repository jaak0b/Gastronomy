using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Printing;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public sealed class TicketActionHandler
{
    private readonly GastronomyAppDbContext dbContext;
    private readonly OrderReader orderReader;
    private readonly OrderStatusProjectionWriter projectionWriter;
    private readonly TicketStateMachine stateMachine;
    private readonly PrintJobEnqueuer printJobEnqueuer;
    private readonly IPrinterFleet printerFleet;
    private readonly HubNotificationDispatcher dispatcher;
    private readonly ResultEnvelope resultEnvelope;
    private readonly ImmediateTransactionRunner transactionRunner = new();

    public TicketActionHandler(
        GastronomyAppDbContext dbContext,
        OrderReader orderReader,
        OrderStatusProjectionWriter projectionWriter,
        TicketStateMachine stateMachine,
        PrintJobEnqueuer printJobEnqueuer,
        IPrinterFleet printerFleet,
        HubNotificationDispatcher dispatcher,
        ResultEnvelope resultEnvelope)
    {
        this.dbContext = dbContext;
        this.orderReader = orderReader;
        this.projectionWriter = projectionWriter;
        this.stateMachine = stateMachine;
        this.printJobEnqueuer = printJobEnqueuer;
        this.printerFleet = printerFleet;
        this.dispatcher = dispatcher;
        this.resultEnvelope = resultEnvelope;
    }

    public async Task<IResult> ResolveAsync(
        Guid orderId,
        Guid ticketId,
        ResolveTicketRequest request,
        Guid? callerStaffMemberId,
        CancellationToken cancellationToken)
    {
        Order? order = await dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

        if (order is null)
        {
            return Results.NotFound();
        }

        if (callerStaffMemberId is not null && order.StaffMemberId != callerStaffMemberId)
        {
            return resultEnvelope.Problem(
                StatusCodes.Status403Forbidden,
                "NotYourOrder",
                "order.notYours");
        }

        LocationTicket? ticket = await dbContext.LocationTickets
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == ticketId && candidate.OrderId == orderId, cancellationToken);

        if (ticket is null)
        {
            return Results.NotFound();
        }

        if (ticket.Status != LocationTicketStatus.Unknown)
        {
            return resultEnvelope.Problem(
                StatusCodes.Status409Conflict,
                "QuestionAlreadyAnswered",
                "ticket.questionAlreadyAnswered");
        }

        LocationTicketStatus target = request.SlipIsOnThePile
            ? LocationTicketStatus.Printed
            : LocationTicketStatus.Queued;

        if (!stateMachine.CanTransition(ticket.Status, target))
        {
            return resultEnvelope.Problem(
                StatusCodes.Status409Conflict,
                "IllegalTicketTransition",
                "ticket.illegalTransition");
        }

        ResolveApplication applied = await transactionRunner.RunAsync(
            dbContext,
            async transactionCancellationToken =>
            {
                LocationTicket tracked = await dbContext.LocationTickets
                    .FirstAsync(candidate => candidate.Id == ticketId, transactionCancellationToken);

                if (tracked.Status != LocationTicketStatus.Unknown)
                {
                    return new TransactionOutcome<ResolveApplication>
                    {
                        Value = new ResolveApplication(false, OrderStatus.Accepted),
                        ShouldCommit = false,
                    };
                }

                tracked.Status = target;
                await dbContext.SaveChangesAsync(transactionCancellationToken);

                OrderStatus status = await projectionWriter.ApplyAsync(
                    dbContext,
                    orderId,
                    transactionCancellationToken);

                return new TransactionOutcome<ResolveApplication>
                {
                    Value = new ResolveApplication(true, status),
                    ShouldCommit = true,
                };
            },
            cancellationToken);

        if (!applied.WasApplied)
        {
            return resultEnvelope.Problem(
                StatusCodes.Status409Conflict,
                "QuestionAlreadyAnswered",
                "ticket.questionAlreadyAnswered");
        }

        OrderStatus orderStatus = applied.OrderStatus;

        if (target == LocationTicketStatus.Queued)
        {
            await printJobEnqueuer.EnqueueWithoutFailingTheCallerAsync(
                ticketId,
                PrintJobKind.Reprint,
                cancellationToken);
        }

        await dispatcher.OnTicketStatusChangedAsync(orderId, ticketId, target, null, cancellationToken);
        await dispatcher.OnOrderStatusChangedAsync(orderId, orderStatus, cancellationToken);

        LoadedOrder loaded = (await orderReader.LoadAsync(dbContext, orderId, cancellationToken))!;

        return Results.Ok(orderReader.DescribeTickets(loaded).First(view => view.TicketId == ticketId));
    }

    public async Task<IResult> ReprintAsync(
        Guid orderId,
        Guid ticketId,
        Guid? callerStaffMemberId,
        CancellationToken cancellationToken)
    {
        Order? order = await dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);

        if (order is null)
        {
            return Results.NotFound();
        }

        if (callerStaffMemberId is not null && order.StaffMemberId != callerStaffMemberId)
        {
            return resultEnvelope.Problem(
                StatusCodes.Status403Forbidden,
                "NotYourOrder",
                "order.notYours");
        }

        LocationTicket? ticket = await dbContext.LocationTickets
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == ticketId && candidate.OrderId == orderId, cancellationToken);

        if (ticket is null)
        {
            return Results.NotFound();
        }

        if (ticket.Status is not (LocationTicketStatus.Failed
            or LocationTicketStatus.Printed
            or LocationTicketStatus.PrintedOnTestPrinter))
        {
            return resultEnvelope.Problem(
                StatusCodes.Status409Conflict,
                "ReprintNotAllowed",
                "ticket.reprintNotAllowed");
        }

        PrintJobEnsured ensured;

        try
        {
            ensured = await printerFleet.EnqueueAsync(ticketId, PrintJobKind.Reprint, cancellationToken);
        }
        catch (UnknownLocationTicketException)
        {
            return Results.NotFound();
        }

        if (!ensured.WasCreated)
        {
            return resultEnvelope.Problem(
                StatusCodes.Status409Conflict,
                "PrintJobAlreadyRunning",
                "ticket.printJobAlreadyRunning");
        }

        LoadedOrder loaded = (await orderReader.LoadAsync(dbContext, orderId, cancellationToken))!;

        return Results.Json(
            orderReader.DescribeTickets(loaded).First(view => view.TicketId == ticketId),
            statusCode: StatusCodes.Status202Accepted);
    }
}

public sealed record ResolveApplication(bool WasApplied, OrderStatus OrderStatus);
