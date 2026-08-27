using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Api.Hub;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class AdminEventSessionEndpoints
{
    public static IEndpointRouteBuilder MapAdminEventSessionEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/admin/event-session", async (
            AdminEventSessionHandler handler,
            CancellationToken cancellationToken) => await handler.CurrentAsync(cancellationToken));

        routes.MapPost("/api/admin/event-session", async (
            StartEventSessionRequest request,
            AdminEventSessionHandler handler,
            CancellationToken cancellationToken) => await handler.StartAsync(request, cancellationToken));

        return routes;
    }
}

public sealed class AdminEventSessionHandler
{
    private readonly GastronomyAppDbContext dbContext;
    private readonly EventSessionStartCoordinator coordinator;
    private readonly EventSessionRefusalDescriber refusalDescriber;
    private readonly HubNotificationDispatcher dispatcher;

    public AdminEventSessionHandler(
        GastronomyAppDbContext dbContext,
        EventSessionStartCoordinator coordinator,
        EventSessionRefusalDescriber refusalDescriber,
        HubNotificationDispatcher dispatcher)
    {
        this.dbContext = dbContext;
        this.coordinator = coordinator;
        this.refusalDescriber = refusalDescriber;
        this.dispatcher = dispatcher;
    }

    public async Task<IResult> CurrentAsync(CancellationToken cancellationToken)
    {
        EventSession? session = await dbContext.EventSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.IsActive, cancellationToken);

        EventSessionStartRefusal? refusal =
            await coordinator.DescribeWhatBlocksStartingAsync(cancellationToken);

        return Results.Ok(new AdminEventSessionView(
            session is null ? null : new EventSessionView(session.Id, session.Name, session.IsPractice),
            refusalDescriber.DescribeBlocks(refusal)));
    }

    public async Task<IResult> StartAsync(StartEventSessionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.Json(
                refusalDescriber.NameMissing(),
                statusCode: StatusCodes.Status400BadRequest);
        }

        EventSessionStartOutcome outcome = await coordinator.StartAsync(
            new EventSessionStartRequest
            {
                Name = request.Name.Trim(),
                IsPractice = request.IsPractice,
                TypedNameConfirmation = request.ConfirmedName?.Trim(),
            },
            cancellationToken);

        if (outcome.Refusal is not null)
        {
            return Results.Json(
                refusalDescriber.Describe(outcome.Refusal),
                statusCode: StatusCodes.Status409Conflict);
        }

        EventSession started = outcome.StartedSession!;

        await dispatcher.PushEventSessionStartedAsync(
            new EventSessionStartedEvent(started.Id, started.Name, started.IsPractice),
            cancellationToken);

        return Results.Json(
            new StartedEventSessionView(
                new EventSessionView(started.Id, started.Name, started.IsPractice),
                started.StartedAtUtc),
            statusCode: StatusCodes.Status201Created);
    }
}

public sealed class EventSessionRefusalDescriber
{
    private const string RefusedCode = "EventSessionStartRefused";
    private const string RefusedMessageKey = "admin.eventSessionStartRefused";

    public EventSessionStartRefusedView Describe(EventSessionStartRefusal refusal)
    {
        return new EventSessionStartRefusedView(
            RefusedCode,
            RefusedMessageKey,
            new Dictionary<string, string>
            {
                ["guardCount"] = refusal.ViolatedGuards.Count.ToString(),
            },
            null,
            DescribeConditions(refusal));
    }

    public EventSessionBlocksView? DescribeBlocks(EventSessionStartRefusal? refusal)
    {
        if (refusal is null)
        {
            return null;
        }

        return new EventSessionBlocksView(
            [.. refusal.ViolatedGuards.Select(guard => guard.ToString())],
            refusal.NonFinalTicketCount,
            refusal.UnansweredUnknownCount,
            [
                .. refusal.LocationsOnTestPrinter.Select(location =>
                    new CatalogLocationView(location.Id, location.Name, location.SortOrder)),
            ],
            DescribeConditions(refusal));
    }

    public EventSessionStartRefusedView NameMissing()
    {
        return new EventSessionStartRefusedView(
            "ValidationFailed",
            "admin.eventSessionNameMissing",
            new Dictionary<string, string>(),
            null,
            []);
    }

    private IReadOnlyList<EventSessionBlockingConditionView> DescribeConditions(EventSessionStartRefusal refusal)
    {
        return [.. refusal.ViolatedGuards.Select(guard => DescribeCondition(guard, refusal))];
    }

    private EventSessionBlockingConditionView DescribeCondition(
        EventSessionStartGuard guard,
        EventSessionStartRefusal refusal)
    {
        return guard switch
        {
            EventSessionStartGuard.NonFinalTicketsRemain => new EventSessionBlockingConditionView(
                guard.ToString(),
                "admin.sessionBlockedByOpenTickets",
                new Dictionary<string, string>
                {
                    ["count"] = refusal.NonFinalTicketCount.ToString(),
                }),
            EventSessionStartGuard.UnansweredUnknownQuestionsRemain => new EventSessionBlockingConditionView(
                guard.ToString(),
                "admin.sessionBlockedByUnansweredQuestions",
                new Dictionary<string, string>
                {
                    ["count"] = refusal.UnansweredUnknownCount.ToString(),
                }),
            EventSessionStartGuard.RecentOrderNeedsTypedConfirmation => new EventSessionBlockingConditionView(
                guard.ToString(),
                "admin.sessionNeedsTypedConfirmation",
                new Dictionary<string, string>()),
            EventSessionStartGuard.ActiveLocationOnTestPrinter => new EventSessionBlockingConditionView(
                guard.ToString(),
                "admin.sessionBlockedByTestPrinter",
                new Dictionary<string, string>
                {
                    ["stations"] = string.Join(", ", refusal.LocationsOnTestPrinter.Select(location => location.Name)),
                }),
            _ => new Never().OfType<EventSessionBlockingConditionView>(guard),
        };
    }
}
