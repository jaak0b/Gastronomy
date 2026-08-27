using System.Text.Json;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Printing;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Printing;

public sealed record PreflightResult(bool IsBlocking, PrinterStatusSnapshot Snapshot, PrintFailureReason? BlockingReason);

public sealed record PendingTestPrint(Guid ProductionLocationId, Guid PrintJobId);

public sealed class PrinterWorker
{
    private readonly PrinterEndpoint endpoint;
    private readonly IPrinterTransport transport;
    private readonly IPrinterWorkerDataAccess dataAccess;
    private readonly IPrintCallbacks callbacks;
    private readonly EscPosSlipRenderer renderer;
    private readonly PrinterWorkerDomainServices domainServices;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<PrinterWorker> logger;
    private readonly StationCircuitBreaker circuitBreaker = new();
    private readonly ReconnectBackoff reconnectBackoff = new();
    private readonly List<Guid> pending = [];
    private readonly List<PendingTestPrint> pendingTestPrints = [];
    private readonly Dictionary<Guid, int> attemptNumbersByJob = [];
    private readonly Lock guard = new();
    private readonly string endpointKey;

    private IPrinterSession? openSession;
    private PrinterStatusSnapshot? cachedStatus;
    private DateTimeOffset lastHeartbeatAtUtc;
    private int unansweredHeartbeats;
    private DateTimeOffset? reconnectNotBeforeUtc;

    public PrinterWorker(
        PrinterEndpoint endpoint,
        IReadOnlyCollection<Guid> servedProductionLocationIds,
        IPrinterTransport transport,
        IPrinterWorkerDataAccess dataAccess,
        IPrintCallbacks callbacks,
        EscPosSlipRenderer renderer,
        PrinterWorkerDomainServices domainServices,
        TimeProvider timeProvider,
        ILogger<PrinterWorker> logger)
    {
        this.endpoint = endpoint;
        this.transport = transport;
        this.dataAccess = dataAccess;
        this.callbacks = callbacks;
        this.renderer = renderer;
        this.domainServices = domainServices;
        this.timeProvider = timeProvider;
        this.logger = logger;
        ServedProductionLocationIds = [.. servedProductionLocationIds];
        lastHeartbeatAtUtc = timeProvider.GetUtcNow();
        endpointKey = domainServices.EndpointKeyBuilder.Build(
            endpoint.TransportKind,
            endpoint.Host ?? string.Empty,
            endpoint.Port == 0 ? null : endpoint.Port,
            endpoint.AgentIdentifier ?? string.Empty);
    }

    public Guid[] ServedProductionLocationIds { get; }

    public PrinterEndpoint Endpoint
    {
        get { return endpoint; }
    }

    public bool IsFaulty
    {
        get { return circuitBreaker.IsTripped; }
    }

    public bool HasOpenSession
    {
        get { return openSession is not null; }
    }

    public IReadOnlyList<Guid> PendingTicketIds
    {
        get
        {
            lock (guard)
            {
                return [.. pending];
            }
        }
    }

    public void Enqueue(Guid locationTicketId)
    {
        lock (guard)
        {
            if (!pending.Contains(locationTicketId))
            {
                pending.Add(locationTicketId);
            }
        }
    }

    public void EnqueueTestPrint(Guid productionLocationId, Guid printJobId)
    {
        lock (guard)
        {
            if (!pendingTestPrints.Any(candidate => candidate.PrintJobId == printJobId))
            {
                pendingTestPrints.Add(new PendingTestPrint(productionLocationId, printJobId));
            }
        }
    }

    public IReadOnlyList<Guid> PendingTestPrintJobIds
    {
        get
        {
            lock (guard)
            {
                return [.. pendingTestPrints.Select(pending => pending.PrintJobId)];
            }
        }
    }

    public TimeSpan NextReconnectDelay()
    {
        return reconnectBackoff.Next();
    }

    public void AcceptStatusSnapshot(PrinterStatusSnapshot snapshot)
    {
        cachedStatus = snapshot;
    }

    public async Task RecoverAtStartupAsync(CancellationToken cancellationToken)
    {
        await dataAccess.MarkPrintingTicketsUnknownAsync(ServedProductionLocationIds, cancellationToken);
        IReadOnlyList<Guid> recoverable = await dataAccess.LoadRecoverableTicketIdsAsync(ServedProductionLocationIds, cancellationToken);
        foreach (Guid locationTicketId in recoverable)
        {
            Enqueue(locationTicketId);
        }
    }

    public async Task<IReadOnlyList<Guid>> ReconnectAsync(CancellationToken cancellationToken)
    {
        circuitBreaker.Reset();
        reconnectBackoff.Reset();
        reconnectNotBeforeUtc = null;
        await CloseSessionAsync();
        await dataAccess.ClearFaultyAtEndpointAsync(ServedProductionLocationIds, cancellationToken);

        foreach (Guid productionLocationId in ServedProductionLocationIds)
        {
            await PushPrinterStatusAsync(productionLocationId, CurrentStatusOrOffline(), false, cancellationToken);
        }

        return ServedProductionLocationIds;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(cancellationToken);
                await HeartbeatAsync(cancellationToken);
                await Task.Delay(RemainingBackoff(), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        await CloseSessionAsync();
    }

    private TimeSpan RemainingBackoff()
    {
        TimeSpan idle = TimeSpan.FromMilliseconds(50);
        if (reconnectNotBeforeUtc is null)
        {
            return idle;
        }

        TimeSpan remaining = reconnectNotBeforeUtc.Value - timeProvider.GetUtcNow();
        return remaining > idle ? remaining : idle;
    }

    public async Task HeartbeatAsync(CancellationToken cancellationToken)
    {
        if (openSession is null || timeProvider.GetUtcNow() - lastHeartbeatAtUtc < endpoint.HeartbeatInterval)
        {
            return;
        }

        lastHeartbeatAtUtc = timeProvider.GetUtcNow();
        try
        {
            PrinterStatusSnapshot snapshot = await openSession.QueryStatusAsync(cancellationToken);
            unansweredHeartbeats = 0;
            AcceptStatusSnapshot(snapshot);
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or ObjectDisposedException)
        {
            unansweredHeartbeats++;
            logger.LogWarning(error, "Heartbeat number {Unanswered} went unanswered at printer endpoint {EndpointKey}.", unansweredHeartbeats, endpointKey);

            if (unansweredHeartbeats >= 2)
            {
                await CloseSessionAsync();
                unansweredHeartbeats = 0;
                await PushOfflineAsync("Two consecutive heartbeats went unanswered.", cancellationToken);
            }
        }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        if (circuitBreaker.IsTripped)
        {
            return;
        }

        if (reconnectNotBeforeUtc is not null && timeProvider.GetUtcNow() < reconnectNotBeforeUtc.Value)
        {
            return;
        }

        if (await TryRunTestPrintAsync(cancellationToken))
        {
            return;
        }

        Guid? head = await NextTicketAsync(cancellationToken);
        if (head is null)
        {
            return;
        }

        TicketLoadResult ticket = await dataAccess.LoadTicketForPrintingAsync(head.Value, cancellationToken);
        ClaimResult claim = await dataAccess.TryClaimAsync(head.Value, cancellationToken);

        switch (claim.Outcome)
        {
            case ClaimOutcome.NoLongerWaiting:
                await EndAsResolvedByHumanAsync(ticket, claim, cancellationToken);
                return;
            case ClaimOutcome.PrinterFaulty:
                logger.LogInformation("Ticket {TicketId} was left unclaimed because its printer is faulty.", head.Value);
                return;
            case ClaimOutcome.Claimed:
                break;
            default:
                new Never().OfType<bool>(claim.Outcome);
                return;
        }

        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        IPrinterSession? session = await EnsureSessionAsync(cancellationToken);
        if (session is null)
        {
            await FinishAsync(
                ticket,
                claim,
                new PrintDispatchResult(PrintAttemptOutcome.Unreachable, 0, CurrentStatusOrOffline(), "The printer could not be reached."),
                PrintAttemptPhase.Connecting,
                startedAt,
                cancellationToken);
            return;
        }

        PreflightResult preflight = await PreflightAsync(session, cancellationToken);
        if (preflight.IsBlocking)
        {
            await FinishAsync(
                ticket,
                claim,
                new PrintDispatchResult(
                    preflight.BlockingReason == PrintFailureReason.PrinterError ? PrintAttemptOutcome.PrinterError : PrintAttemptOutcome.Blocked,
                    0,
                    preflight.Snapshot,
                    $"Pre-flight refused the job: {preflight.BlockingReason}."),
                PrintAttemptPhase.PreflightCheck,
                startedAt,
                cancellationToken);
            return;
        }

        RenderedSlip slip = Render(ticket);
        int processId = await dataAccess.AllocateProcessIdAsync(endpointKey, cancellationToken);

        PrintPayload payload = new(
            processId,
            slip.Bytes,
            slip.RenderedText,
            ticket.Kind,
            ticket.LocationSequenceNumber,
            ticket.ReprintCount,
            ticket.ProductionLocationId,
            ticket.ProductionLocationName);

        PrintDispatchResult dispatch;
        try
        {
            dispatch = await session.SendJobAsync(payload, cancellationToken);
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or ObjectDisposedException)
        {
            logger.LogWarning(error, "Sending ticket {TicketId} failed at printer endpoint {EndpointKey}.", ticket.LocationTicketId, endpointKey);
            await CloseSessionAsync();
            dispatch = new PrintDispatchResult(PrintAttemptOutcome.SocketDropped, 0, CurrentStatusOrOffline(), error.Message);
        }

        await FinishAsync(ticket, claim, dispatch, PrintAttemptPhase.AwaitingEcho, startedAt, cancellationToken);
    }

    private async Task<bool> TryRunTestPrintAsync(CancellationToken cancellationToken)
    {
        PendingTestPrint pending;
        lock (guard)
        {
            if (pendingTestPrints.Count == 0)
            {
                return false;
            }

            pending = pendingTestPrints[0];
            pendingTestPrints.RemoveAt(0);
        }

        Guid productionLocationId = pending.ProductionLocationId;
        DateTimeOffset startedAt = timeProvider.GetUtcNow();
        IPrinterSession? session = await EnsureSessionAsync(cancellationToken);
        if (session is null)
        {
            await dataAccess.RecordAttemptAsync(
                BuildAttempt(
                    pending.PrintJobId,
                    PrintAttemptOutcome.Unreachable,
                    PrintAttemptPhase.Connecting,
                    0,
                    CurrentStatusOrOffline(),
                    "The printer could not be reached, so the test slip was not sent.",
                    startedAt,
                    timeProvider.GetUtcNow()),
                cancellationToken);

            await dataAccess.FailJobOnlyAsync(pending.PrintJobId, PrintFailureReason.Unreachable, cancellationToken);
            return true;
        }

        TestPrintLoadResult testPrint = await dataAccess.LoadTestPrintAsync(productionLocationId, cancellationToken);
        RenderedSlip slip = renderer.RenderTestSlip(new TestSlipRenderRequest(
            testPrint.ProductionLocationName,
            testPrint.SlipLanguage,
            timeProvider.GetUtcNow(),
            TimeZoneInfo.Local,
            testPrint.StationCardUrl));

        int processId = await dataAccess.AllocateProcessIdAsync(endpointKey, cancellationToken);
        PrintDispatchResult dispatch = await session.SendJobAsync(
            new PrintPayload(processId, slip.Bytes, slip.RenderedText, PrintJobKind.Test, 0, 0, productionLocationId, testPrint.ProductionLocationName),
            cancellationToken);

        await dataAccess.RecordAttemptAsync(
            BuildAttempt(pending.PrintJobId, dispatch.Outcome, PrintAttemptPhase.AwaitingEcho, dispatch.BytesWritten, dispatch.StatusAtEnd, dispatch.Detail, startedAt, timeProvider.GetUtcNow()),
            cancellationToken);
        await dataAccess.WritePrinterStatusAsync(productionLocationId, dispatch.StatusAtEnd, cancellationToken);
        await PushPrinterStatusAsync(productionLocationId, dispatch.StatusAtEnd, false, cancellationToken);
        return true;
    }

    private async Task<Guid?> NextTicketAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> snapshot = PendingTicketIds;
        if (snapshot.Count == 0)
        {
            return null;
        }

        IReadOnlyList<Guid> ordered = await dataAccess.OrderQueueAsync(snapshot, cancellationToken);
        foreach (Guid candidate in ordered)
        {
            if (snapshot.Contains(candidate))
            {
                return candidate;
            }
        }

        return snapshot[0];
    }

    private RenderedSlip Render(TicketLoadResult ticket)
    {
        if (ticket.Kind == PrintJobKind.Test)
        {
            return renderer.RenderTestSlip(new TestSlipRenderRequest(
                ticket.ProductionLocationName,
                ticket.SlipLanguage,
                timeProvider.GetUtcNow(),
                TimeZoneInfo.Local,
                ticket.StationCardUrl));
        }

        SlipRenderRequest request = new(
            ticket.ProductionLocationName,
            ticket.SlipLanguage,
            ticket.LocationSequenceNumber,
            ticket.GlobalOrderNumber,
            ticket.TableLabel,
            ticket.ServerName,
            new DateTimeOffset(DateTime.SpecifyKind(ticket.OrderCreatedAtUtc, DateTimeKind.Utc)),
            TimeZoneInfo.Local,
            [.. ticket.Lines.Select(line => new SlipLine(line.Quantity, line.ItemName, line.LineNote))],
            ticket.OrderNote,
            ticket.AlsoGoesToStationNames,
            ticket.ChosenStationNameIfDifferent);

        return ticket.Kind == PrintJobKind.Reprint
            ? renderer.RenderReprintSlip(request, timeProvider.GetUtcNow(), TimeZoneInfo.Local)
            : renderer.RenderInitialSlip(request);
    }

    private async Task<IPrinterSession?> EnsureSessionAsync(CancellationToken cancellationToken)
    {
        if (openSession is not null)
        {
            return openSession;
        }

        try
        {
            openSession = await transport.ConnectAsync(endpoint, cancellationToken);
            reconnectBackoff.Reset();
            reconnectNotBeforeUtc = null;
            unansweredHeartbeats = 0;
            lastHeartbeatAtUtc = timeProvider.GetUtcNow();
            return openSession;
        }
        catch (Exception error) when (error is PrinterUnreachableException or IOException or InvalidOperationException)
        {
            TimeSpan retryIn = reconnectBackoff.Next();
            reconnectNotBeforeUtc = timeProvider.GetUtcNow() + retryIn;
            logger.LogWarning(error, "Connecting to printer endpoint {EndpointKey} failed; retrying in {RetryIn}.", endpointKey, retryIn);
            await PushOfflineAsync(error.Message, cancellationToken);
            return null;
        }
    }

    private async Task<PreflightResult> PreflightAsync(IPrinterSession session, CancellationToken cancellationToken)
    {
        PrinterStatusSnapshot snapshot;
        if (cachedStatus is not null && timeProvider.GetUtcNow() - cachedStatus.ObservedAt < endpoint.HeartbeatInterval)
        {
            snapshot = cachedStatus;
        }
        else
        {
            snapshot = await session.QueryStatusAsync(cancellationToken);
        }

        PrintFailureReason? blockingReason = snapshot switch
        {
            { IsPaperEnd: true } => PrintFailureReason.PaperEnd,
            { IsCoverOpen: true } => PrintFailureReason.CoverOpen,
            { IsInErrorState: true } => PrintFailureReason.PrinterError,
            _ => null,
        };

        return new PreflightResult(blockingReason is not null, snapshot, blockingReason);
    }

    private async Task EndAsResolvedByHumanAsync(TicketLoadResult ticket, ClaimResult claim, CancellationToken cancellationToken)
    {
        Remove(ticket.LocationTicketId);
        DateTimeOffset now = timeProvider.GetUtcNow();

        await dataAccess.RecordAttemptAsync(
            BuildAttempt(
                claim.PrintJobId,
                PrintAttemptOutcome.Blocked,
                PrintAttemptPhase.Connecting,
                0,
                CurrentStatusOrOffline(),
                "The ticket was no longer waiting, so the socket was not touched.",
                now,
                now),
            cancellationToken);

        LocationTicketStatus statusTheHumanLeft = await dataAccess.FailJobOnlyAsync(
            claim.PrintJobId,
            PrintFailureReason.TicketResolvedByHuman,
            cancellationToken);

        await callbacks.OnTicketStatusChangedAsync(
            ticket.OrderId,
            ticket.LocationTicketId,
            statusTheHumanLeft,
            PrintFailureReason.TicketResolvedByHuman,
            cancellationToken);
        await PushOrderProjectionAsync(ticket.OrderId, cancellationToken);
    }

    private async Task FinishAsync(
        TicketLoadResult ticket,
        ClaimResult claim,
        PrintDispatchResult dispatch,
        PrintAttemptPhase phase,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        PrintOutcomeMapping mapping = MapOutcome(dispatch, ticket.LocationTicketId);

        await dataAccess.RecordAttemptAsync(
            BuildAttempt(
                claim.PrintJobId,
                dispatch.Outcome,
                phase,
                dispatch.BytesWritten,
                dispatch.StatusAtEnd,
                dispatch.Detail,
                startedAt,
                timeProvider.GetUtcNow()),
            cancellationToken);

        if (!domainServices.TicketStateMachine.CanTransition(LocationTicketStatus.Printing, mapping.TicketStatus))
        {
            throw new InvalidOperationException(
                $"The ticket state machine refuses Printing to {mapping.TicketStatus}, which the job sequence requires for ticket {ticket.LocationTicketId}.");
        }

        PrintOutcomeApplied applied = await dataAccess.ApplyOutcomeAsync(
            new PrintOutcomeApplication
            {
                LocationTicketId = ticket.LocationTicketId,
                PrintJobId = claim.PrintJobId,
                Outcome = dispatch.Outcome,
                BytesWritten = dispatch.BytesWritten,
                StatusAtEnd = dispatch.StatusAtEnd,
                JobStatus = mapping.JobStatus,
                TicketStatus = mapping.TicketStatus,
                FailureReason = null,
            },
            cancellationToken);

        await dataAccess.WritePrinterStatusAsync(ticket.ProductionLocationId, dispatch.StatusAtEnd, cancellationToken);
        await callbacks.OnTicketStatusChangedAsync(ticket.OrderId, ticket.LocationTicketId, applied.TicketStatus, null, cancellationToken);
        await PushPrinterStatusAsync(ticket.ProductionLocationId, dispatch.StatusAtEnd, false, cancellationToken);

        if (mapping.JobStatus == PrintJobStatus.Unknown)
        {
            await ReQueryAfterUnknownAsync(ticket.ProductionLocationId, cancellationToken);
        }

        bool stillWaiting = !applied.TicketWasTakenByHuman
            && mapping.ShouldRetryAutomatically
            && !await GiveUpAsync(ticket, claim, dispatch, mapping, cancellationToken);

        if (stillWaiting)
        {
            Enqueue(ticket.LocationTicketId);
        }
        else
        {
            Remove(ticket.LocationTicketId);
        }

        await PushOrderProjectionAsync(ticket.OrderId, cancellationToken);
        await TripBreakerIfNeededAsync(dispatch, mapping, cancellationToken);
    }

    private async Task ReQueryAfterUnknownAsync(Guid productionLocationId, CancellationToken cancellationToken)
    {
        if (openSession is null)
        {
            return;
        }

        try
        {
            PrinterStatusSnapshot reQueried = await openSession.QueryStatusAsync(cancellationToken);
            AcceptStatusSnapshot(reQueried);
            await dataAccess.WritePrinterStatusAsync(productionLocationId, reQueried, cancellationToken);
            await PushPrinterStatusAsync(productionLocationId, reQueried, false, cancellationToken);
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or ObjectDisposedException)
        {
            logger.LogWarning(error, "The status re-query after an unknown outcome failed at printer endpoint {EndpointKey}.", endpointKey);
            await CloseSessionAsync();
        }
    }

    private PrintOutcomeMapping MapOutcome(PrintDispatchResult dispatch, Guid locationTicketId)
    {
        try
        {
            return domainServices.RetryPolicy.Map(dispatch.Outcome, transport.Kind, dispatch.BytesWritten);
        }
        catch (InvalidOperationException error)
        {
            logger.LogError(
                error,
                "The retry policy has no row for outcome {Outcome} with {BytesWritten} bytes written, so ticket {TicketId} is recorded as unknown.",
                dispatch.Outcome,
                dispatch.BytesWritten,
                locationTicketId);

            return new PrintOutcomeMapping
            {
                JobStatus = PrintJobStatus.Unknown,
                TicketStatus = LocationTicketStatus.Unknown,
                ShouldRetryAutomatically = false,
            };
        }
    }

    private async Task<bool> GiveUpAsync(
        TicketLoadResult ticket,
        ClaimResult claim,
        PrintDispatchResult dispatch,
        PrintOutcomeMapping mapping,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SuspensionPeriod> suspensions =
            await dataAccess.LoadSuspensionPeriodsAsync(ticket.ProductionLocationId, transport.Kind, cancellationToken);

        GiveUpWindowEvaluation evaluation = domainServices.GiveUpWindowCalculator.Evaluate(
            ticket.CreatedAtUtc,
            timeProvider.GetUtcNow().UtcDateTime,
            mapping.TicketStatus,
            suspensions);

        if (!evaluation.HasReachedGiveUpWindow && !evaluation.HasReachedOuterBound)
        {
            return false;
        }

        PrintFailureReason reason = FailureReasonFor(dispatch);
        await dataAccess.FailTicketAsync(ticket.LocationTicketId, claim.PrintJobId, reason, cancellationToken);
        await callbacks.OnTicketStatusChangedAsync(
            ticket.OrderId,
            ticket.LocationTicketId,
            LocationTicketStatus.Failed,
            reason,
            cancellationToken);

        return true;
    }

    private PrintFailureReason FailureReasonFor(PrintDispatchResult dispatch)
    {
        return dispatch.Outcome switch
        {
            PrintAttemptOutcome.Blocked => dispatch.StatusAtEnd.IsCoverOpen ? PrintFailureReason.CoverOpen : PrintFailureReason.PaperEnd,
            PrintAttemptOutcome.PrinterError => PrintFailureReason.PrinterError,
            PrintAttemptOutcome.Unreachable => PrintFailureReason.Unreachable,
            PrintAttemptOutcome.SocketDropped => PrintFailureReason.SocketDropped,
            PrintAttemptOutcome.Timeout => PrintFailureReason.Timeout,
            PrintAttemptOutcome.Confirmed => PrintFailureReason.PrinterError,
            _ => new Never().OfType<PrintFailureReason>(dispatch.Outcome),
        };
    }

    private async Task TripBreakerIfNeededAsync(
        PrintDispatchResult dispatch,
        PrintOutcomeMapping mapping,
        CancellationToken cancellationToken)
    {
        if (!circuitBreaker.RecordOutcome(dispatch.Outcome, mapping.JobStatus))
        {
            return;
        }

        logger.LogError("Printer endpoint {EndpointKey} stopped making sense and its circuit breaker tripped.", endpointKey);
        await dataAccess.FailAllWaitingAtEndpointAsync(ServedProductionLocationIds, PrintFailureReason.StationFaulty, cancellationToken);

        lock (guard)
        {
            pending.Clear();
        }

        foreach (Guid productionLocationId in ServedProductionLocationIds)
        {
            await PushPrinterStatusAsync(productionLocationId, dispatch.StatusAtEnd, true, cancellationToken);
        }
    }

    private async Task PushOrderProjectionAsync(Guid orderId, CancellationToken cancellationToken)
    {
        OrderTicketStatuses statuses = await dataAccess.LoadOrderTicketStatusesAsync(orderId, cancellationToken);
        OrderStatus recalculated = domainServices.OrderStatusCalculator.Calculate(statuses.TicketStatuses, statuses.IsPracticeSession);

        if (recalculated == statuses.CurrentStatus)
        {
            return;
        }

        await dataAccess.WriteOrderStatusAsync(orderId, recalculated, cancellationToken);
        await callbacks.OnOrderStatusChangedAsync(orderId, recalculated, cancellationToken);
    }

    private async Task PushOfflineAsync(string detail, CancellationToken cancellationToken)
    {
        PrinterStatusSnapshot offline = new(false, false, false, false, false, detail, timeProvider.GetUtcNow());
        cachedStatus = null;

        foreach (Guid productionLocationId in ServedProductionLocationIds)
        {
            await dataAccess.WritePrinterStatusAsync(productionLocationId, offline, cancellationToken);
            await PushPrinterStatusAsync(productionLocationId, offline, false, cancellationToken);
        }
    }

    private async Task PushPrinterStatusAsync(
        Guid productionLocationId,
        PrinterStatusSnapshot snapshot,
        bool isFaulty,
        CancellationToken cancellationToken)
    {
        int waiting = await dataAccess.CountWaitingTicketsAsync(productionLocationId, cancellationToken);
        await callbacks.OnPrinterStatusChangedAsync(productionLocationId, snapshot, isFaulty, waiting, cancellationToken);
    }

    private PrintAttempt BuildAttempt(
        Guid printJobId,
        PrintAttemptOutcome outcome,
        PrintAttemptPhase phase,
        int bytesWritten,
        PrinterStatusSnapshot snapshot,
        string transportDetail,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt)
    {
        int nextAttemptNumber;
        lock (guard)
        {
            attemptNumbersByJob.TryGetValue(printJobId, out int previous);
            nextAttemptNumber = previous + 1;
            attemptNumbersByJob[printJobId] = nextAttemptNumber;
        }

        return new PrintAttempt
        {
            Id = Guid.NewGuid(),
            PrintJobId = printJobId,
            AttemptNumber = nextAttemptNumber,
            Outcome = outcome,
            Phase = phase,
            BytesWritten = bytesWritten,
            TransportDetail = transportDetail,
            PrinterStatusSnapshotJson = JsonSerializer.Serialize(snapshot),
            StartedAtUtc = startedAt.UtcDateTime,
            EndedAtUtc = endedAt.UtcDateTime,
        };
    }

    private PrinterStatusSnapshot CurrentStatusOrOffline()
    {
        return cachedStatus ?? new PrinterStatusSnapshot(false, false, false, false, false, "No status has been observed yet.", timeProvider.GetUtcNow());
    }

    private void Remove(Guid locationTicketId)
    {
        lock (guard)
        {
            pending.Remove(locationTicketId);
        }
    }

    private async Task CloseSessionAsync()
    {
        if (openSession is null)
        {
            return;
        }

        IPrinterSession closing = openSession;
        openSession = null;
        await closing.DisposeAsync();
    }
}
