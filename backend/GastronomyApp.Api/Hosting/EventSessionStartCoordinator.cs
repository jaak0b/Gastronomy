using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Hosting;

public sealed record EventSessionStartOutcome(
    EventSession? StartedSession,
    EventSessionStartRefusal? Refusal);

public sealed class EventSessionStartCoordinator
{
    private readonly GastronomyAppDbContext dbContext;
    private readonly IDbContextFactory<GastronomyAppDbContext> contextFactory;
    private readonly EventSessionStartService startService;
    private readonly IClock clock;
    private readonly ImmediateTransactionRunner transactionRunner = new();

    public EventSessionStartCoordinator(
        GastronomyAppDbContext dbContext,
        IDbContextFactory<GastronomyAppDbContext> contextFactory,
        EventSessionStartService startService,
        IClock clock)
    {
        this.dbContext = dbContext;
        this.contextFactory = contextFactory;
        this.startService = startService;
        this.clock = clock;
    }

    public async Task<EventSessionStartOutcome> StartAsync(
        EventSessionStartRequest request,
        CancellationToken cancellationToken)
    {
        Result<EventSessionStartDecision, EventSessionStartRefusal> decision =
            await startService.StartAsync(request, cancellationToken);

        if (!decision.IsSuccess)
        {
            return new EventSessionStartOutcome(null, decision.Failure);
        }

        EventSession started = await ApplyAsync(decision.Value, cancellationToken);

        return new EventSessionStartOutcome(started, null);
    }

    public async Task<EventSessionStartRefusal?> DescribeWhatBlocksStartingAsync(CancellationToken cancellationToken)
    {
        await using GastronomyAppDbContext probeContext = await contextFactory.CreateDbContextAsync(cancellationToken);

        EventSessionStartService probeService = new(
            new EventSessionRepository(probeContext),
            new EventSessionStartGuardReader(probeContext),
            clock);

        Result<EventSessionStartDecision, EventSessionStartRefusal> probe = await probeService.StartAsync(
            new EventSessionStartRequest
            {
                Name = string.Empty,
                IsPractice = false,
                TypedNameConfirmation = null,
            },
            cancellationToken);

        return probe.IsSuccess ? null : probe.Failure;
    }

    private Task<EventSession> ApplyAsync(EventSessionStartDecision decision, CancellationToken cancellationToken)
    {
        return transactionRunner.RunAsync(
            dbContext,
            async transactionCancellationToken =>
            {
                if (decision.PreviousSessionToEnd is not null)
                {
                    EventSession? previous = await dbContext.EventSessions.FirstOrDefaultAsync(
                        session => session.Id == decision.PreviousSessionToEnd.Id,
                        transactionCancellationToken);

                    if (previous is not null)
                    {
                        previous.EndedAtUtc = decision.PreviousSessionToEnd.EndedAtUtc;
                        previous.IsActive = decision.PreviousSessionToEnd.IsActive;
                    }
                }

                dbContext.EventSessions.Add(decision.SessionToStart);
                await dbContext.SaveChangesAsync(transactionCancellationToken);

                return new TransactionOutcome<EventSession>
                {
                    Value = decision.SessionToStart,
                    ShouldCommit = true,
                };
            },
            cancellationToken);
    }
}
