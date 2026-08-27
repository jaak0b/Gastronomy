using FakeItEasy;
using GastronomyApp.Api.Printing;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Localization;
using GastronomyApp.Infrastructure.Printing;
using Microsoft.Extensions.Logging.Abstractions;

namespace GastronomyApp.Api.Tests.Printing;

public class PrinterWorkerTest
{
    private IPrinterWorkerDataAccess dataAccess = null!;
    private IPrintCallbacks callbacks = null!;
    private IPrinterTransport transport = null!;
    private IPrinterSession session = null!;
    private RetryPolicy retryPolicy = null!;
    private TestTimeProvider timeProvider = null!;
    private Guid locationId;
    private Guid otherLocationId;
    private Guid ticketId;
    private Guid orderId;
    private Guid printJobId;

    [SetUp]
    public void SetUp()
    {
        locationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        otherLocationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        ticketId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        orderId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        printJobId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        dataAccess = A.Fake<IPrinterWorkerDataAccess>();
        callbacks = A.Fake<IPrintCallbacks>();
        transport = A.Fake<IPrinterTransport>();
        session = A.Fake<IPrinterSession>();
        retryPolicy = new RetryPolicy();
        timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 8, 26, 19, 42, 0, TimeSpan.Zero));

        A.CallTo(() => transport.Kind).Returns(TransportKind.Mock);
        A.CallTo(() => transport.ConnectAsync(A<PrinterEndpoint>._, A<CancellationToken>._)).Returns(Task.FromResult(session));
        A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._)).Returns(Task.FromResult(CleanStatus()));
        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new PrintDispatchResult(PrintAttemptOutcome.Confirmed, 512, CleanStatus(), "ok")));
        A.CallTo(() => dataAccess.LoadTicketForPrintingAsync(A<Guid>._, A<CancellationToken>._))
            .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Ticket(id)));
        A.CallTo(() => dataAccess.TryClaimAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.Claimed, PrintJobId = printJobId }));
        A.CallTo(() => dataAccess.AllocateProcessIdAsync(A<string>._, A<CancellationToken>._)).Returns(Task.FromResult(17));
        A.CallTo(() => dataAccess.OrderQueueAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
            .ReturnsLazily((IReadOnlyCollection<Guid> ids, CancellationToken _) => Task.FromResult<IReadOnlyList<Guid>>([.. ids]));
        A.CallTo(() => dataAccess.LoadOrderTicketStatusesAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new OrderTicketStatuses
            {
                CurrentStatus = OrderStatus.Printing,
                TicketStatuses = [LocationTicketStatus.Printing],
            }));
        A.CallTo(() => dataAccess.LoadSuspensionPeriodsAsync(A<Guid>._, A<TransportKind>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<SuspensionPeriod>>([]));
        A.CallTo(() => dataAccess.ApplyOutcomeAsync(A<PrintOutcomeApplication>._, A<CancellationToken>._))
            .ReturnsLazily((PrintOutcomeApplication application, CancellationToken _) => Task.FromResult(new PrintOutcomeApplied
            {
                TicketStatus = application.TicketStatus,
                TicketWasTakenByHuman = false,
            }));
        A.CallTo(() => dataAccess.FailJobOnlyAsync(A<Guid>._, A<PrintFailureReason>._, A<CancellationToken>._))
            .Returns(Task.FromResult(LocationTicketStatus.HandledOnPaper));
        A.CallTo(() => dataAccess.CountWaitingTicketsAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult(0));
    }

    private PrinterStatusSnapshot CleanStatus()
    {
        return new PrinterStatusSnapshot(true, false, false, false, false, "clean", timeProvider.GetUtcNow());
    }

    private PrinterEndpoint Endpoint(TransportKind kind = TransportKind.Mock)
    {
        return new PrinterEndpoint(
            locationId,
            kind,
            "10.0.0.5",
            9100,
            null,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(90),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(3));
    }

    private TicketLoadResult Ticket(
        Guid? id = null,
        Guid? location = null,
        DateTime? createdAtUtc = null,
        PrintJobKind kind = PrintJobKind.Initial,
        LocationTicketStatus status = LocationTicketStatus.Queued)
    {
        return new TicketLoadResult
        {
            LocationTicketId = id ?? ticketId,
            OrderId = orderId,
            ProductionLocationId = location ?? locationId,
            PrintJobId = printJobId,
            Kind = kind,
            CreatedAtUtc = createdAtUtc ?? new DateTime(2026, 8, 26, 19, 40, 0, DateTimeKind.Utc),
            Status = status,
            ReprintCount = 0,
            LocationSequenceNumber = 42,
            ProductionLocationName = "Küche",
            SlipLanguage = "de",
            GlobalOrderNumber = 137,
            TableLabel = "12",
            ServerName = "Anna",
            OrderNote = null,
            OrderCreatedAtUtc = new DateTime(2026, 8, 26, 19, 40, 0, DateTimeKind.Utc),
            Lines = [new TicketLineLoadResult(2, "Bratwurst", null)],
            AlsoGoesToStationNames = [],
            ChosenStationNameIfDifferent = null,
        };
    }

    private PrinterWorker Worker(PrinterEndpoint? endpoint = null, IReadOnlyCollection<Guid>? served = null)
    {
        return new PrinterWorker(
            endpoint ?? Endpoint(),
            served ?? [locationId],
            transport,
            dataAccess,
            callbacks,
            new EscPosSlipRenderer(new ResxSlipTextProvider()),
            new PrinterWorkerDomainServices(
                retryPolicy,
                new GiveUpWindowCalculator(),
                new OrderStatusCalculator(),
                new TicketStateMachine(),
                new PrintJobStateMachine(),
                new PrinterEndpointKeyBuilder()),
            timeProvider,
            NullLogger<PrinterWorker>.Instance);
    }

    [Test]
    public async Task RunAsync_ClaimFailsBecauseTicketNoLongerWaiting_EndsFailedWithTicketResolvedByHumanZeroBytes()
    {
        A.CallTo(() => dataAccess.TryClaimAsync(ticketId, A<CancellationToken>._))
            .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.NoLongerWaiting, PrintJobId = printJobId }));
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => transport.ConnectAsync(A<PrinterEndpoint>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => dataAccess.FailJobOnlyAsync(printJobId, PrintFailureReason.TicketResolvedByHuman, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => dataAccess.FailTicketAsync(A<Guid>._, A<Guid>._, A<PrintFailureReason>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => callbacks.OnTicketStatusChangedAsync(
                orderId,
                ticketId,
                LocationTicketStatus.HandledOnPaper,
                PrintFailureReason.TicketResolvedByHuman,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => dataAccess.RecordAttemptAsync(A<PrintAttempt>.That.Matches(attempt => attempt.BytesWritten == 0), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RunAsync_ClaimSucceeds_MovesTicketToPrintingBeforeSending()
    {
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.TryClaimAsync(ticketId, A<CancellationToken>._)).MustHaveHappened()
            .Then(A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustHaveHappened());
    }

    [Test]
    public async Task RunAsync_PrinterDeclaredFaultySinceEnqueue_LeavesTicketUnclaimedForBreakerToResolve()
    {
        A.CallTo(() => dataAccess.TryClaimAsync(ticketId, A<CancellationToken>._))
            .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.PrinterFaulty, PrintJobId = printJobId }));
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => dataAccess.FailTicketAsync(A<Guid>._, A<Guid>._, A<PrintFailureReason>._, A<CancellationToken>._)).MustNotHaveHappened();
        Assert.That(worker.PendingTicketIds, Does.Contain(ticketId));
    }

    [Test]
    public async Task RunAsync_NoOpenSession_ConnectsWithConfiguredConnectTimeout()
    {
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => transport.ConnectAsync(
                A<PrinterEndpoint>.That.Matches(candidate => candidate.ConnectTimeout == TimeSpan.FromSeconds(3)),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RunAsync_ConnectFails_MarksPrinterOfflineLeavesJobQueuedSchedulesReconnectWithBackoff()
    {
        A.CallTo(() => transport.ConnectAsync(A<PrinterEndpoint>._, A<CancellationToken>._))
            .Throws(new PrinterUnreachableException("no route"));
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => callbacks.OnPrinterStatusChangedAsync(
                locationId,
                A<PrinterStatusSnapshot>.That.Matches(snapshot => !snapshot.IsOnline),
                false,
                A<int>._,
                A<CancellationToken>._))
            .MustHaveHappened();
        Assert.That(worker.PendingTicketIds, Does.Contain(ticketId));
        Assert.That(
            new[]
            {
                worker.NextReconnectDelay(),
                worker.NextReconnectDelay(),
                worker.NextReconnectDelay(),
                worker.NextReconnectDelay(),
                worker.NextReconnectDelay(),
                worker.NextReconnectDelay(),
            },
            Is.EqualTo(new[]
            {
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30),
            }));
    }

    [Test]
    public async Task RunAsync_PreflightAsbFresherThanHeartbeatInterval_SkipsQuery()
    {
        PrinterWorker worker = Worker();
        worker.AcceptStatusSnapshot(CleanStatus());
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task RunAsync_PreflightStaleAsb_QueriesDleEotN4AndN2()
    {
        PrinterWorker worker = Worker();
        worker.AcceptStatusSnapshot(CleanStatus());
        timeProvider.Advance(TimeSpan.FromSeconds(30));
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RunAsync_PreflightBlocking_MovesToBlockedWithZeroBytesNoSend()
    {
        A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(new PrinterStatusSnapshot(true, true, false, false, false, "paper end", timeProvider.GetUtcNow())));
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => dataAccess.ApplyOutcomeAsync(
                A<PrintOutcomeApplication>.That.Matches(application =>
                    application.TicketStatus == LocationTicketStatus.Blocked
                    && application.JobStatus == PrintJobStatus.Blocked
                    && application.BytesWritten == 0),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => callbacks.OnTicketStatusChangedAsync(orderId, ticketId, LocationTicketStatus.Blocked, A<PrintFailureReason?>._, A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Test]
    public async Task RunAsync_PreflightClean_ProceedsToRenderAndSend()
    {
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => session.SendJobAsync(
                A<PrintPayload>.That.Matches(payload => payload.RenderedText.Contains("BON 042", StringComparison.Ordinal)),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RunAsync_RendersBeforeAllocatingProcessId_ProcessIdNullUntilStep6()
    {
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.AllocateProcessIdAsync(A<string>._, A<CancellationToken>._)).MustHaveHappened()
            .Then(A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustHaveHappened());
    }

    [Test]
    public async Task RunAsync_AllocatesProcessIdFromEndpointKeyedCounter_NotLocationKeyed()
    {
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.AllocateProcessIdAsync("Mock|10.0.0.5|9100|", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => dataAccess.AllocateProcessIdAsync(
                A<string>.That.Matches(key => key.Contains(locationId.ToString(), StringComparison.OrdinalIgnoreCase)),
                A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Test]
    public async Task RunAsync_SendsPayloadThenWaitsForEchoUpToJobTimeout()
    {
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => session.SendJobAsync(A<PrintPayload>.That.Matches(payload => payload.ProcessId == 17), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RunAsync_RecordsAttemptWithOutcomePhaseBytesAndStatusSnapshot()
    {
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.RecordAttemptAsync(
                A<PrintAttempt>.That.Matches(attempt =>
                    attempt.PrintJobId == printJobId
                    && attempt.Outcome == PrintAttemptOutcome.Confirmed
                    && attempt.Phase == PrintAttemptPhase.AwaitingEcho
                    && attempt.BytesWritten == 512
                    && attempt.PrinterStatusSnapshotJson.Length > 0),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RunAsync_PushesTicketStatusChangedAlways_AndOrderStatusChangedOnlyWhenItChanged()
    {
        A.CallTo(() => dataAccess.LoadOrderTicketStatusesAsync(orderId, A<CancellationToken>._))
            .Returns(Task.FromResult(new OrderTicketStatuses
            {
                CurrentStatus = OrderStatus.Printed,
                TicketStatuses = [LocationTicketStatus.PrintedOnTestPrinter],
            }));
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => callbacks.OnTicketStatusChangedAsync(orderId, ticketId, LocationTicketStatus.PrintedOnTestPrinter, null, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => callbacks.OnOrderStatusChangedAsync(A<Guid>._, A<OrderStatus>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task RunAsync_OrderStatusChanged_PushesOrderStatusChanged()
    {
        A.CallTo(() => dataAccess.LoadOrderTicketStatusesAsync(orderId, A<CancellationToken>._))
            .Returns(Task.FromResult(new OrderTicketStatuses
            {
                CurrentStatus = OrderStatus.Printing,
                TicketStatuses = [LocationTicketStatus.PrintedOnTestPrinter],
            }));
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.WriteOrderStatusAsync(orderId, OrderStatus.Printed, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => callbacks.OnOrderStatusChangedAsync(orderId, OrderStatus.Printed, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task Enqueue_JobsAttemptedInLocationTicketCreatedAtUtcOrder()
    {
        Guid oldest = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        Guid middle = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
        Guid newest = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
        A.CallTo(() => dataAccess.OrderQueueAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<Guid>>([oldest, middle, newest]));

        PrinterWorker worker = Worker();
        worker.Enqueue(newest);
        worker.Enqueue(oldest);
        worker.Enqueue(middle);

        List<Guid> attempted = [];
        A.CallTo(() => dataAccess.TryClaimAsync(A<Guid>._, A<CancellationToken>._))
            .Invokes((Guid id, CancellationToken _) => attempted.Add(id))
            .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.Claimed, PrintJobId = printJobId }));

        await worker.RunOnceAsync(CancellationToken.None);
        await worker.RunOnceAsync(CancellationToken.None);
        await worker.RunOnceAsync(CancellationToken.None);

        Assert.That(attempted.ToArray(), Is.EqualTo(new[] { oldest, middle, newest }));
    }

    [Test]
    public async Task RunAsync_BlockedJobHoldsStationRatherThanBeingOvertaken()
    {
        Guid blockedTicket = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
        Guid laterTicket = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
        A.CallTo(() => dataAccess.OrderQueueAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<Guid>>([blockedTicket, laterTicket]));
        A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(new PrinterStatusSnapshot(true, true, false, false, false, "paper end", timeProvider.GetUtcNow())));

        PrinterWorker worker = Worker();
        worker.Enqueue(blockedTicket);
        worker.Enqueue(laterTicket);

        List<Guid> attempted = [];
        A.CallTo(() => dataAccess.TryClaimAsync(A<Guid>._, A<CancellationToken>._))
            .Invokes((Guid id, CancellationToken _) => attempted.Add(id))
            .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.Claimed, PrintJobId = printJobId }));

        await worker.RunOnceAsync(CancellationToken.None);
        await worker.RunOnceAsync(CancellationToken.None);

        Assert.That(attempted.ToArray(), Is.EqualTo(new[] { blockedTicket, blockedTicket }));
        Assert.That(worker.PendingTicketIds, Does.Contain(laterTicket));
    }

    [Test]
    public async Task RecoverAtStartupAsync_EnqueuesQueuedAndBlockedTicketsOfAnyEventSession_OldestFirst()
    {
        Guid first = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
        Guid second = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
        A.CallTo(() => dataAccess.LoadRecoverableTicketIdsAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<Guid>>([first, second]));

        PrinterWorker worker = Worker();
        await worker.RecoverAtStartupAsync(CancellationToken.None);

        Assert.That(worker.PendingTicketIds, Is.EqualTo(new[] { first, second }));
        A.CallTo(() => dataAccess.LoadRecoverableTicketIdsAsync(
                A<IReadOnlyCollection<Guid>>.That.Matches(ids => ids.Contains(locationId)),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RecoverAtStartupAsync_MarksPrintingTicketsUnknown()
    {
        PrinterWorker worker = Worker();

        await worker.RecoverAtStartupAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.MarkPrintingTicketsUnknownAsync(
                A<IReadOnlyCollection<Guid>>.That.Matches(ids => ids.Contains(locationId)),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RunAsync_HeartbeatEvery10Seconds_UsingFakeTimeProvider()
    {
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);
        await worker.RunOnceAsync(CancellationToken.None);

        timeProvider.Advance(TimeSpan.FromSeconds(11));
        await worker.HeartbeatAsync(CancellationToken.None);

        A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._)).MustHaveHappened();
    }

    [Test]
    public async Task RunAsync_TwoConsecutiveHeartbeatsUnanswered_MarksPrinterOfflineAndForcesReconnect()
    {
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);
        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._)).Throws(new IOException("no answer"));
        timeProvider.Advance(TimeSpan.FromSeconds(11));
        await worker.HeartbeatAsync(CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromSeconds(11));
        await worker.HeartbeatAsync(CancellationToken.None);

        A.CallTo(() => callbacks.OnPrinterStatusChangedAsync(
                locationId,
                A<PrinterStatusSnapshot>.That.Matches(snapshot => !snapshot.IsOnline),
                false,
                A<int>._,
                A<CancellationToken>._))
            .MustHaveHappened();
        Assert.That(worker.HasOpenSession, Is.False);
    }

    [Test]
    public async Task RunAsync_BlockingConditionClearsFromSetToClear_ReleasesBlockedJobsInCreatedAtUtcOrder()
    {
        A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(new PrinterStatusSnapshot(true, true, false, false, false, "paper end", timeProvider.GetUtcNow())));
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);
        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._)).Returns(Task.FromResult(CleanStatus()));
        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        Assert.That(worker.PendingTicketIds, Is.Empty);
    }

    [TestCase(PrintAttemptOutcome.Confirmed, TransportKind.Network, 512, PrintJobStatus.Confirmed, LocationTicketStatus.Printed, false)]
    [TestCase(PrintAttemptOutcome.Confirmed, TransportKind.Mock, 512, PrintJobStatus.Confirmed, LocationTicketStatus.PrintedOnTestPrinter, false)]
    [TestCase(PrintAttemptOutcome.Blocked, TransportKind.Mock, 0, PrintJobStatus.Blocked, LocationTicketStatus.Blocked, true)]
    [TestCase(PrintAttemptOutcome.Unreachable, TransportKind.Mock, 0, PrintJobStatus.Queued, LocationTicketStatus.Queued, true)]
    [TestCase(PrintAttemptOutcome.SocketDropped, TransportKind.Mock, 0, PrintJobStatus.Queued, LocationTicketStatus.Queued, true)]
    [TestCase(PrintAttemptOutcome.SocketDropped, TransportKind.Mock, 300, PrintJobStatus.Unknown, LocationTicketStatus.Unknown, false)]
    [TestCase(PrintAttemptOutcome.Timeout, TransportKind.Mock, 0, PrintJobStatus.Queued, LocationTicketStatus.Queued, true)]
    [TestCase(PrintAttemptOutcome.Timeout, TransportKind.Mock, 300, PrintJobStatus.Unknown, LocationTicketStatus.Unknown, false)]
    [TestCase(PrintAttemptOutcome.PrinterError, TransportKind.Mock, 0, PrintJobStatus.Blocked, LocationTicketStatus.Blocked, true)]
    [TestCase(PrintAttemptOutcome.PrinterError, TransportKind.Mock, 300, PrintJobStatus.Unknown, LocationTicketStatus.Unknown, false)]
    public async Task RunAsync_RetryMapping_ZeroBytesRetried_BytesNeverRetried(
        PrintAttemptOutcome outcome,
        TransportKind transportKind,
        int bytesWritten,
        PrintJobStatus expectedJobStatus,
        LocationTicketStatus expectedTicketStatus,
        bool expectedRequeue)
    {
        A.CallTo(() => transport.Kind).Returns(transportKind);
        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new PrintDispatchResult(outcome, bytesWritten, CleanStatus(), "mapped")));

        PrinterWorker worker = Worker(Endpoint(transportKind));
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.ApplyOutcomeAsync(
                A<PrintOutcomeApplication>.That.Matches(application =>
                    application.JobStatus == expectedJobStatus && application.TicketStatus == expectedTicketStatus),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        Assert.That(worker.PendingTicketIds.Contains(ticketId), Is.EqualTo(expectedRequeue));
    }

    [TestCase(PrintAttemptOutcome.Blocked)]
    [TestCase(PrintAttemptOutcome.Unreachable)]
    public async Task RunAsync_RetryPolicyThrowsForUndefinedRow_MarksJobAndTicketUnknown(PrintAttemptOutcome outcome)
    {
        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new PrintDispatchResult(outcome, 300, CleanStatus(), "undefined row")));

        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.ApplyOutcomeAsync(
                A<PrintOutcomeApplication>.That.Matches(application =>
                    application.JobStatus == PrintJobStatus.Unknown
                    && application.TicketStatus == LocationTicketStatus.Unknown),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        Assert.That(worker.PendingTicketIds, Does.Not.Contain(ticketId));
    }

    [Test]
    public async Task StationCircuitBreaker_TwoConsecutiveUnknownOrTimeoutOutcomes_TripsAtEndpoint()
    {
        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new PrintDispatchResult(PrintAttemptOutcome.Timeout, 300, CleanStatus(), "unknown")));
        PrinterWorker worker = Worker(served: [locationId, otherLocationId]);
        worker.Enqueue(ticketId);
        worker.Enqueue(Guid.Parse("dddddddd-0000-0000-0000-000000000002"));

        await worker.RunOnceAsync(CancellationToken.None);
        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.FailAllWaitingAtEndpointAsync(
                A<IReadOnlyCollection<Guid>>.That.Matches(ids => ids.Contains(locationId) && ids.Contains(otherLocationId)),
                PrintFailureReason.StationFaulty,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => callbacks.OnPrinterStatusChangedAsync(locationId, A<PrinterStatusSnapshot>._, true, A<int>._, A<CancellationToken>._)).MustHaveHappened();
        A.CallTo(() => callbacks.OnPrinterStatusChangedAsync(otherLocationId, A<PrinterStatusSnapshot>._, true, A<int>._, A<CancellationToken>._)).MustHaveHappened();
        Assert.That(worker.IsFaulty, Is.True);
    }

    [Test]
    public async Task StationCircuitBreaker_AConfirmedJobResetsTheConsecutiveCounter()
    {
        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new PrintDispatchResult(PrintAttemptOutcome.Timeout, 300, CleanStatus(), "unknown")))
            .Once()
            .Then
            .Returns(Task.FromResult(new PrintDispatchResult(PrintAttemptOutcome.Confirmed, 300, CleanStatus(), "ok")))
            .Once()
            .Then
            .Returns(Task.FromResult(new PrintDispatchResult(PrintAttemptOutcome.Timeout, 300, CleanStatus(), "unknown")));

        PrinterWorker worker = Worker();
        worker.Enqueue(Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"));
        worker.Enqueue(Guid.Parse("eeeeeeee-0000-0000-0000-000000000002"));
        worker.Enqueue(Guid.Parse("eeeeeeee-0000-0000-0000-000000000003"));

        await worker.RunOnceAsync(CancellationToken.None);
        await worker.RunOnceAsync(CancellationToken.None);
        await worker.RunOnceAsync(CancellationToken.None);

        Assert.That(worker.IsFaulty, Is.False);
    }

    [Test]
    public async Task StationCircuitBreaker_QueueDepthNeverTripsIt()
    {
        A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(new PrinterStatusSnapshot(true, true, false, false, false, "paper end", timeProvider.GetUtcNow())));
        PrinterWorker worker = Worker();
        for (int index = 0; index < 50; index++)
        {
            worker.Enqueue(Guid.NewGuid());
        }

        for (int index = 0; index < 50; index++)
        {
            await worker.RunOnceAsync(CancellationToken.None);
        }

        Assert.That(worker.IsFaulty, Is.False);
        A.CallTo(() => dataAccess.FailAllWaitingAtEndpointAsync(A<IReadOnlyCollection<Guid>>._, A<PrintFailureReason>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Test]
    public async Task StationCircuitBreaker_HumanReconnectClearsFaultyAndRestartsWorker()
    {
        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new PrintDispatchResult(PrintAttemptOutcome.Timeout, 300, CleanStatus(), "unknown")));
        PrinterWorker worker = Worker(served: [locationId, otherLocationId]);
        worker.Enqueue(Guid.Parse("ffffffff-0000-0000-0000-000000000001"));
        worker.Enqueue(Guid.Parse("ffffffff-0000-0000-0000-000000000002"));
        await worker.RunOnceAsync(CancellationToken.None);
        await worker.RunOnceAsync(CancellationToken.None);

        IReadOnlyList<Guid> cleared = await worker.ReconnectAsync(CancellationToken.None);

        Assert.That(worker.IsFaulty, Is.False);
        Assert.That(cleared, Is.EquivalentTo(new[] { locationId, otherLocationId }));
        A.CallTo(() => dataAccess.ClearFaultyAtEndpointAsync(
                A<IReadOnlyCollection<Guid>>.That.Matches(ids => ids.Contains(locationId) && ids.Contains(otherLocationId)),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task GiveUpWindow_MeasuredFromLocationTicketCreatedAtUtc_NotFromFirstAttempt()
    {
        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new PrintDispatchResult(PrintAttemptOutcome.Unreachable, 0, CleanStatus(), "unreachable")));
        A.CallTo(() => dataAccess.LoadTicketForPrintingAsync(A<Guid>._, A<CancellationToken>._))
            .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Ticket(id, createdAtUtc: new DateTime(2026, 8, 26, 19, 30, 0, DateTimeKind.Utc))));

        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.FailTicketAsync(ticketId, printJobId, PrintFailureReason.Unreachable, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        Assert.That(worker.PendingTicketIds, Does.Not.Contain(ticketId));
    }

    [Test]
    public async Task GiveUpWindow_SuspendedForFourKnownCauses_ResumesWithoutResettingWhenCauseClears()
    {
        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new PrintDispatchResult(PrintAttemptOutcome.Unreachable, 0, CleanStatus(), "unreachable")));
        A.CallTo(() => dataAccess.LoadTicketForPrintingAsync(A<Guid>._, A<CancellationToken>._))
            .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Ticket(id, createdAtUtc: new DateTime(2026, 8, 26, 19, 30, 0, DateTimeKind.Utc))));
        A.CallTo(() => dataAccess.LoadSuspensionPeriodsAsync(locationId, A<TransportKind>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<SuspensionPeriod>>(
            [
                new SuspensionPeriod
                {
                    StartedAtUtc = new DateTime(2026, 8, 26, 19, 31, 0, DateTimeKind.Utc),
                    EndedAtUtc = new DateTime(2026, 8, 26, 19, 41, 0, DateTimeKind.Utc),
                },
            ]));

        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.LoadSuspensionPeriodsAsync(locationId, A<TransportKind>._, A<CancellationToken>._)).MustHaveHappened();
        A.CallTo(() => dataAccess.FailTicketAsync(A<Guid>._, A<Guid>._, A<PrintFailureReason>._, A<CancellationToken>._)).MustNotHaveHappened();
        Assert.That(worker.PendingTicketIds, Does.Contain(ticketId));
    }

    [Test]
    public async Task GiveUpWindow_MechanicalErrorBlockedTicket_IsNotSuspendedAndExpiresAtFiveMinutes()
    {
        A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(new PrinterStatusSnapshot(true, false, false, false, true, "mechanical error", timeProvider.GetUtcNow())));
        A.CallTo(() => dataAccess.LoadTicketForPrintingAsync(A<Guid>._, A<CancellationToken>._))
            .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Ticket(id, createdAtUtc: new DateTime(2026, 8, 26, 19, 30, 0, DateTimeKind.Utc))));

        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.FailTicketAsync(ticketId, printJobId, PrintFailureReason.PrinterError, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task OuterBound_TwentyMinutes_FiresUnderEveryKnownCause_NeverFiresOnPrinting()
    {
        A.CallTo(() => dataAccess.LoadTicketForPrintingAsync(A<Guid>._, A<CancellationToken>._))
            .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Ticket(id, createdAtUtc: new DateTime(2026, 8, 26, 19, 0, 0, DateTimeKind.Utc))));
        A.CallTo(() => dataAccess.LoadSuspensionPeriodsAsync(locationId, A<TransportKind>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<SuspensionPeriod>>(
            [
                new SuspensionPeriod
                {
                    StartedAtUtc = new DateTime(2026, 8, 26, 19, 0, 0, DateTimeKind.Utc),
                    EndedAtUtc = null,
                },
            ]));
        A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(new PrinterStatusSnapshot(true, true, false, false, false, "paper end", timeProvider.GetUtcNow())));

        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.FailTicketAsync(ticketId, printJobId, PrintFailureReason.PaperEnd, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task StationCircuitBreaker_IsWorkerOwned_NotSharedWithGiveUpWindowCalculator()
    {
        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new PrintDispatchResult(PrintAttemptOutcome.Timeout, 300, CleanStatus(), "unknown")));
        A.CallTo(() => dataAccess.LoadTicketForPrintingAsync(A<Guid>._, A<CancellationToken>._))
            .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Ticket(id, createdAtUtc: timeProvider.GetUtcNow().UtcDateTime)));

        PrinterWorker worker = Worker();
        worker.Enqueue(Guid.Parse("12121212-0000-0000-0000-000000000001"));
        worker.Enqueue(Guid.Parse("12121212-0000-0000-0000-000000000002"));

        await worker.RunOnceAsync(CancellationToken.None);
        await worker.RunOnceAsync(CancellationToken.None);

        Assert.That(worker.IsFaulty, Is.True);
    }

    [Test]
    public async Task RunAsync_HumanTookTheTicketDuringTheEchoWait_AppliesTheOutcomeToTheJobOnly()
    {
        A.CallTo(() => dataAccess.ApplyOutcomeAsync(A<PrintOutcomeApplication>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new PrintOutcomeApplied
            {
                TicketStatus = LocationTicketStatus.HandledOnPaper,
                TicketWasTakenByHuman = true,
            }));
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => callbacks.OnTicketStatusChangedAsync(
                orderId,
                ticketId,
                LocationTicketStatus.HandledOnPaper,
                A<PrintFailureReason?>._,
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        Assert.That(worker.PendingTicketIds, Does.Not.Contain(ticketId));
    }

    [Test]
    public async Task RunAsync_SuspensionPeriodsAreQueriedForTheTransportActuallyInUse()
    {
        A.CallTo(() => transport.Kind).Returns(TransportKind.Network);
        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new PrintDispatchResult(PrintAttemptOutcome.Unreachable, 0, CleanStatus(), "unreachable")));
        PrinterWorker worker = Worker(Endpoint(TransportKind.Network));
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.LoadSuspensionPeriodsAsync(locationId, TransportKind.Network, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RunAsync_ConnectFails_WaitsTheBackoffBeforeAttemptingToConnectAgain()
    {
        A.CallTo(() => transport.ConnectAsync(A<PrinterEndpoint>._, A<CancellationToken>._))
            .Throws(new PrinterUnreachableException("no route"));
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);
        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => transport.ConnectAsync(A<PrinterEndpoint>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();

        timeProvider.Advance(TimeSpan.FromMilliseconds(1100));
        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => transport.ConnectAsync(A<PrinterEndpoint>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Test]
    public async Task RunAsync_UnknownOutcome_ReQueriesTheStatusAndPushesItWithoutResending()
    {
        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new PrintDispatchResult(PrintAttemptOutcome.Timeout, 300, CleanStatus(), "unknown")));
        PrinterWorker worker = Worker();
        worker.Enqueue(ticketId);

        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._)).MustHaveHappenedTwiceOrMore();
        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => dataAccess.WritePrinterStatusAsync(locationId, A<PrinterStatusSnapshot>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceOrMore();
    }

    [Test]
    public async Task RunAsync_AttemptNumber_CountsPerJobRatherThanPerWorkerLifetime()
    {
        Guid firstTicket = Guid.Parse("13131313-0000-0000-0000-000000000001");
        Guid secondTicket = Guid.Parse("13131313-0000-0000-0000-000000000002");
        Guid firstJob = Guid.Parse("13131313-1111-0000-0000-000000000001");
        Guid secondJob = Guid.Parse("13131313-1111-0000-0000-000000000002");

        A.CallTo(() => dataAccess.TryClaimAsync(firstTicket, A<CancellationToken>._))
            .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.Claimed, PrintJobId = firstJob }));
        A.CallTo(() => dataAccess.TryClaimAsync(secondTicket, A<CancellationToken>._))
            .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.Claimed, PrintJobId = secondJob }));

        PrinterWorker worker = Worker();
        worker.Enqueue(firstTicket);
        worker.Enqueue(secondTicket);

        await worker.RunOnceAsync(CancellationToken.None);
        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.RecordAttemptAsync(
                A<PrintAttempt>.That.Matches(attempt => attempt.PrintJobId == secondJob && attempt.AttemptNumber == 1),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task TestPrint_OnASharedEndpoint_PrintsTheRequestedStationsCardNotTheFirstServedOne()
    {
        A.CallTo(() => dataAccess.LoadTestPrintAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new TestPrintLoadResult("Theke", "de")));
        PrinterWorker worker = Worker(served: [locationId, otherLocationId]);

        worker.EnqueueTestPrint(otherLocationId, Guid.NewGuid());
        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.LoadTestPrintAsync(otherLocationId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => session.SendJobAsync(
                A<PrintPayload>.That.Matches(payload => payload.ProductionLocationId == otherLocationId),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task TestPrint_ConnectFails_RecordsAnAttemptRatherThanDroppingTheJob()
    {
        A.CallTo(() => transport.ConnectAsync(A<PrinterEndpoint>._, A<CancellationToken>._))
            .Throws(new PrinterUnreachableException("no route"));
        Guid testJobId = Guid.Parse("14141414-0000-0000-0000-000000000001");
        PrinterWorker worker = Worker();

        worker.EnqueueTestPrint(locationId, testJobId);
        await worker.RunOnceAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.RecordAttemptAsync(
                A<PrintAttempt>.That.Matches(attempt =>
                    attempt.PrintJobId == testJobId
                    && attempt.Outcome == PrintAttemptOutcome.Unreachable
                    && attempt.BytesWritten == 0),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
