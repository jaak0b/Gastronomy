using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.RateLimiting;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Printing;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/api/orders").RequireAuthorization().RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

        group.MapPost(string.Empty, async (
            PlaceOrderRequest request,
            HttpContext httpContext,
            CallerIdentity callerIdentity,
            OrderPlacementHandler handler,
            CancellationToken cancellationToken) =>
        {
            DeviceCaller caller = callerIdentity.ReadDevice(httpContext.User)!;
            return await handler.PlaceAsync(request, caller, cancellationToken);
        });

        group.MapGet("/mine", async (
            HttpContext httpContext,
            CallerIdentity callerIdentity,
            OrderQueryHandler handler,
            CancellationToken cancellationToken) =>
        {
            DeviceCaller caller = callerIdentity.ReadDevice(httpContext.User)!;
            return await handler.ListForStaffMemberAsync(caller.StaffMemberId, cancellationToken);
        });

        group.MapGet("/{orderId:guid}", async (
            Guid orderId,
            HttpContext httpContext,
            CallerIdentity callerIdentity,
            OrderQueryHandler handler,
            CancellationToken cancellationToken) =>
        {
            DeviceCaller caller = callerIdentity.ReadDevice(httpContext.User)!;
            return await handler.DetailAsync(orderId, caller.StaffMemberId, cancellationToken);
        });

        group.MapPost("/{orderId:guid}/tickets/{ticketId:guid}/resolve", async (
            Guid orderId,
            Guid ticketId,
            ResolveTicketRequest request,
            HttpContext httpContext,
            CallerIdentity callerIdentity,
            TicketActionHandler handler,
            CancellationToken cancellationToken) =>
        {
            DeviceCaller caller = callerIdentity.ReadDevice(httpContext.User)!;
            return await handler.ResolveAsync(orderId, ticketId, request, caller.StaffMemberId, cancellationToken);
        });

        group.MapPost("/{orderId:guid}/tickets/{ticketId:guid}/reprint", async (
            Guid orderId,
            Guid ticketId,
            HttpContext httpContext,
            CallerIdentity callerIdentity,
            TicketActionHandler handler,
            CancellationToken cancellationToken) =>
        {
            DeviceCaller caller = callerIdentity.ReadDevice(httpContext.User)!;
            return await handler.ReprintAsync(orderId, ticketId, caller.StaffMemberId, cancellationToken);
        });

        return routes;
    }
}

public sealed class OrderPlacementHandler
{
    private readonly GastronomyAppDbContext dbContext;
    private readonly OrderAcceptanceTransaction acceptanceTransaction;
    private readonly OrderReader orderReader;
    private readonly OrderStatusProjectionWriter projectionWriter;
    private readonly PrintJobEnqueuer printJobEnqueuer;
    private readonly HubNotificationDispatcher dispatcher;
    private readonly ResultEnvelope resultEnvelope;

    public OrderPlacementHandler(
        GastronomyAppDbContext dbContext,
        OrderAcceptanceTransaction acceptanceTransaction,
        OrderReader orderReader,
        OrderStatusProjectionWriter projectionWriter,
        PrintJobEnqueuer printJobEnqueuer,
        HubNotificationDispatcher dispatcher,
        ResultEnvelope resultEnvelope)
    {
        this.dbContext = dbContext;
        this.acceptanceTransaction = acceptanceTransaction;
        this.orderReader = orderReader;
        this.projectionWriter = projectionWriter;
        this.printJobEnqueuer = printJobEnqueuer;
        this.dispatcher = dispatcher;
        this.resultEnvelope = resultEnvelope;
    }

    public async Task<IResult> PlaceAsync(
        PlaceOrderRequest request,
        DeviceCaller caller,
        CancellationToken cancellationToken)
    {
        int expectedTotalCents = request.ExpectedTotalCents ?? 0;

        OrderAcceptanceRequest acceptanceRequest = new()
        {
            ClientOrderId = request.ClientOrderId,
            StaffMemberId = caller.StaffMemberId,
            DeviceId = caller.DeviceId,
            TableLabel = request.TableLabel ?? string.Empty,
            Note = request.Note,
            Lines =
            [
                .. (request.Lines ?? []).Select(line => new OrderAcceptanceLineRequest
                {
                    CatalogItemId = line.CatalogItemId,
                    Quantity = line.Quantity,
                    Note = line.Note,
                    StationId = line.StationId,
                }),
            ],
        };

        Result<OrderAcceptanceResult, OrderValidationFailure> acceptance =
            await acceptanceTransaction.AcceptAsync(acceptanceRequest, cancellationToken);

        if (!acceptance.IsSuccess)
        {
            return resultEnvelope.ToResult(resultEnvelope.Describe(acceptance.Failure));
        }

        Guid orderId = acceptance.Value.Order.Id;

        if (acceptance.Value.WasAlreadyAccepted)
        {
            LoadedOrder? existing = await orderReader.LoadAsync(dbContext, orderId, cancellationToken);

            if (existing is null)
            {
                return resultEnvelope.Problem(
                    StatusCodes.Status409Conflict,
                    "SubmissionIdReused",
                    "order.submissionIdReused");
            }

            if (!new SubmissionComparison().Matches(request, existing))
            {
                return resultEnvelope.Problem(
                    StatusCodes.Status409Conflict,
                    "SubmissionIdReused",
                    "order.submissionIdReused");
            }

            foreach (LocationTicket waiting in existing.Tickets)
            {
                await printJobEnqueuer.EnqueueWithoutFailingTheCallerAsync(
                    waiting.Id,
                    PrintJobKind.Initial,
                    cancellationToken);
            }

            return Results.Json(
                orderReader.Describe(existing, expectedTotalCents),
                statusCode: StatusCodes.Status200OK);
        }

        await projectionWriter.WriteAsync(dbContext, orderId, cancellationToken);

        LoadedOrder placed = (await orderReader.LoadAsync(dbContext, orderId, cancellationToken))!;

        foreach (LocationTicket ticket in placed.Tickets)
        {
            await printJobEnqueuer.EnqueueWithoutFailingTheCallerAsync(
                ticket.Id,
                PrintJobKind.Initial,
                cancellationToken);
        }

        PlacedOrderView view = orderReader.Describe(placed, expectedTotalCents);

        await dispatcher.PushOrderAcceptedAsync(
            caller.StaffMemberId,
            new OrderAcceptedEvent(
                view.OrderId,
                view.GlobalOrderNumber,
                placed.Order.TableLabel,
                view.TotalCents,
                view.Tickets),
            cancellationToken);

        return Results.Json(view, statusCode: StatusCodes.Status201Created);
    }
}

public sealed class SubmissionComparison
{
    public bool Matches(PlaceOrderRequest request, LoadedOrder existing)
    {
        if (!string.Equals(request.TableLabel ?? string.Empty, existing.Order.TableLabel, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(request.Note ?? string.Empty, existing.Order.Note ?? string.Empty, StringComparison.Ordinal))
        {
            return false;
        }

        List<OrderLineRequest> requestLines = [.. request.Lines ?? []];

        if (requestLines.Count != existing.Lines.Count)
        {
            return false;
        }

        List<OrderLine> remaining = [.. existing.Lines];

        foreach (OrderLineRequest line in requestLines)
        {
            OrderLine? match = remaining.FirstOrDefault(candidate =>
                candidate.CatalogItemId == line.CatalogItemId
                && candidate.Quantity == line.Quantity
                && string.Equals(candidate.Note ?? string.Empty, line.Note ?? string.Empty, StringComparison.Ordinal)
                && candidate.ChosenStationId == line.StationId);

            if (match is null)
            {
                return false;
            }

            remaining.Remove(match);
        }

        return remaining.Count == 0;
    }
}
