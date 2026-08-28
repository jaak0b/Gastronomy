using GastronomyApp.Api.Options;
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
  private IPrinterDriver driver = null!;
  private IPrinterSession session = null!;
  private RetryPolicy retryPolicy = null!;
  private TestTimeProvider timeProvider = null!;
  private Guid stationId;
  private Guid otherStationId;
  private Guid stationOrderId;
  private Guid orderId;
  private Guid printJobId;
  private Guid printerId = Guid.Parse("7f8e9d0c-1b2a-4c3d-8e5f-6a7b8c9d0e1f");

  [SetUp]
  public void SetUp()
  {
    stationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    otherStationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    stationOrderId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    orderId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    printJobId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    dataAccess = A.Fake<IPrinterWorkerDataAccess>();
    callbacks = A.Fake<IPrintCallbacks>();
    driver = A.Fake<IPrinterDriver>();
    session = A.Fake<IPrinterSession>();
    retryPolicy = new RetryPolicy();
    timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 8, 26, 19, 42, 0, TimeSpan.Zero));

    A.CallTo(() => driver.HeartbeatInterval).Returns(TimeSpan.FromSeconds(10));
    A.CallTo(() => driver.ConnectTimeout).Returns(TimeSpan.FromSeconds(3));
    A.CallTo(() => driver.JobTimeout).Returns(TimeSpan.FromSeconds(90));
    A.CallTo(() => driver.ConnectAsync(A<Printer>._, A<CancellationToken>._)).Returns(Task.FromResult(session));
    A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._)).Returns(Task.FromResult(CleanStatus()));
    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
        .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Confirmed, 512, CleanStatus(), "ok")));
    A.CallTo(() => dataAccess.LoadPrintJobAsync(A<Guid>._, A<CancellationToken>._))
        .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Job(id)));
    A.CallTo(() => dataAccess.TryClaimAsync(A<Guid>._, A<CancellationToken>._))
        .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.Claimed, PrintJobId = printJobId }));
    A.CallTo(() => dataAccess.AllocatePrinterJobIdAsync(A<CancellationToken>._)).Returns(Task.FromResult(17));
    A.CallTo(() => dataAccess.OrderQueueAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
        .ReturnsLazily((IReadOnlyCollection<Guid> ids, CancellationToken _) => Task.FromResult<IReadOnlyList<Guid>>([.. ids]));
    A.CallTo(() => dataAccess.LoadOrderPrintJobStatusesAsync(A<Guid>._, A<CancellationToken>._))
        .Returns(Task.FromResult(new OrderPrintJobStatuses
        {
          CurrentStatus = OrderStatus.Printing,
          PrintJobStatuses = [PrintJobStatus.Sending],
        }));
    A.CallTo(() => dataAccess.LoadSuspensionPeriodsAsync(A<Guid>._, A<CancellationToken>._))
        .Returns(Task.FromResult<IReadOnlyList<SuspensionPeriod>>([]));
    A.CallTo(() => dataAccess.ApplyOutcomeAsync(A<PrintOutcomeApplication>._, A<CancellationToken>._))
        .ReturnsLazily((PrintOutcomeApplication application, CancellationToken _) => Task.FromResult(new PrintOutcomeApplied
        {
          PrintJobStatus = application.JobStatus,
          WasHandledOnPaper = false,
        }));
    A.CallTo(() => dataAccess.CountWaitingPrintJobsAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult(0));
  }

  private PrinterStatusSnapshot CleanStatus()
  {
    return new PrinterStatusSnapshot(true, false, false, false, false, "clean", timeProvider.GetUtcNow());
  }

  private Printer APrinter()
  {
    return new TestPrinter
    {
      Id = printerId,
      Name = "Drucker Küche",
    };
  }

  private PrintJobLoadResult Job(
      Guid? id = null,
      Guid? station = null,
      DateTime? createdAtUtc = null,
      int copyNumber = 0,
      PrintJobStatus status = PrintJobStatus.Queued)
  {
    return new PrintJobLoadResult
    {
      PrintJobId = id ?? printJobId,
      StationOrderId = stationOrderId,
      OrderId = orderId,
      StationId = station ?? stationId,
      CopyNumber = copyNumber,
      CreatedAtUtc = createdAtUtc ?? new DateTime(2026, 8, 26, 19, 40, 0, DateTimeKind.Utc),
      Status = status,
      StationOrderNumber = 42,
      StationName = "Küche",
      GlobalOrderNumber = 137,
      TableName = "12",
      StaffMemberName = "Anna",
      OrderNote = null,
      OrderCreatedAtUtc = new DateTime(2026, 8, 26, 19, 40, 0, DateTimeKind.Utc),
      Items = [new PrintJobItemLoadResult("Bratwurst", null), new PrintJobItemLoadResult("Bratwurst", null)],
      AlsoGoesToStationNames = [],
    };
  }

  private readonly AppLanguage language = new();

  private PrinterWorker Worker(IReadOnlyCollection<Guid>? served = null)
  {
    return new PrinterWorker(
        APrinter(),
        served ?? [stationId],
        driver,
        dataAccess,
        callbacks,
        new EscPosSlipRenderer(new ResxSlipTextProvider()),
        new PrinterWorkerDomainServices(
            retryPolicy,
            new GiveUpWindowCalculator(),
            new OrderStatusCalculator(),
            new PrintJobStateMachine()),
        timeProvider,
        language,
        NullLogger<PrinterWorker>.Instance);
  }

  [Test]
  public async Task TestPrint_LaptopSetToEnglish_PrintsTheSlipInEnglish()
  {
    language.Current = "en";
    PrintPayload? sent = null;
    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
        .Invokes((PrintPayload payload, CancellationToken _) => sent = payload);
    PrinterWorker worker = Worker();

    worker.EnqueueTestPrint(Guid.NewGuid());
    await worker.RunOnceAsync(CancellationToken.None);

    Assert.That(sent!.RenderedText, Does.Contain("TEST SLIP"));
  }

  [Test]
  public async Task TestPrint_LaptopSetToGerman_PrintsTheSlipInGerman()
  {
    language.Current = "de";
    PrintPayload? sent = null;
    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
        .Invokes((PrintPayload payload, CancellationToken _) => sent = payload);
    PrinterWorker worker = Worker();

    worker.EnqueueTestPrint(Guid.NewGuid());
    await worker.RunOnceAsync(CancellationToken.None);

    Assert.That(sent!.RenderedText, Does.Contain("TESTBON"));
  }

  [Test]
  public async Task RunAsync_ClaimFailsBecauseTheJobIsNoLongerWaiting_TouchesNothingAndReportsTheStatusItFound()
  {
    A.CallTo(() => dataAccess.LoadPrintJobAsync(A<Guid>._, A<CancellationToken>._))
        .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Job(id, status: PrintJobStatus.HandledOnPaper)));
    A.CallTo(() => dataAccess.TryClaimAsync(A<Guid>._, A<CancellationToken>._))
        .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.NoLongerWaiting, PrintJobId = printJobId }));
    PrinterWorker worker = Worker();
    worker.Enqueue(printJobId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => driver.ConnectAsync(A<Printer>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => dataAccess.FailPrintJobAsync(A<Guid>._, A<PrintFailureReason>._, A<CancellationToken>._))
        .MustNotHaveHappened();
    A.CallTo(() => callbacks.OnPrintJobStatusChangedAsync(
            orderId,
            stationOrderId,
            PrintJobStatus.HandledOnPaper,
            null,
            A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
    Assert.That(worker.PendingPrintJobIds, Does.Not.Contain(printJobId));
  }

  [Test]
  public async Task RunAsync_ClaimSucceeds_MovesTicketToPrintingBeforeSending()
  {
    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => dataAccess.TryClaimAsync(stationOrderId, A<CancellationToken>._)).MustHaveHappened()
        .Then(A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustHaveHappened());
  }

  [Test]
  public async Task RunAsync_PrinterDeclaredFaultySinceEnqueue_LeavesTicketUnclaimedForBreakerToResolve()
  {
    A.CallTo(() => dataAccess.TryClaimAsync(stationOrderId, A<CancellationToken>._))
        .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.PrinterFaulty, PrintJobId = printJobId }));
    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustNotHaveHappened();
    Assert.That(worker.PendingPrintJobIds, Does.Contain(stationOrderId));
  }

  [Test]
  public async Task RunAsync_NoOpenSession_ConnectsWithConfiguredConnectTimeout()
  {
    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => driver.ConnectAsync(
            A<Printer>.That.Matches(candidate => candidate.Id == printerId),
            A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RunAsync_ConnectFails_MarksPrinterOfflineLeavesJobQueuedSchedulesReconnectWithBackoff()
  {
    A.CallTo(() => driver.ConnectAsync(A<Printer>._, A<CancellationToken>._))
        .Throws(new PrinterUnreachableException("no route"));
    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => callbacks.OnPrinterStatusChangedAsync(
            printerId,
            A<IReadOnlyList<Guid>>.That.Matches(ids => ids.Contains(stationId)),
            A<PrinterStatusSnapshot>.That.Matches(snapshot => !snapshot.IsOnline),
            false,
            A<int>._,
            A<CancellationToken>._))
        .MustHaveHappened();
    Assert.That(worker.PendingPrintJobIds, Does.Contain(stationOrderId));
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
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task RunAsync_PreflightStaleAsb_QueriesDleEotN4AndN2()
  {
    PrinterWorker worker = Worker();
    worker.AcceptStatusSnapshot(CleanStatus());
    timeProvider.Advance(TimeSpan.FromSeconds(30));
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RunAsync_PreflightBlocking_MovesToBlockedWithZeroBytesNoSend()
  {
    A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._))
        .Returns(Task.FromResult(new PrinterStatusSnapshot(true, true, false, false, false, "paper end", timeProvider.GetUtcNow())));
    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => dataAccess.ApplyOutcomeAsync(
            A<PrintOutcomeApplication>.That.Matches(application =>
                 application.JobStatus == PrintJobStatus.Blocked
                && application.BytesWritten == 0),
            A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
    A.CallTo(() => callbacks.OnPrintJobStatusChangedAsync(orderId, stationOrderId, PrintJobStatus.Blocked, A<PrintFailureReason?>._, A<CancellationToken>._))
        .MustHaveHappened();
  }

  [Test]
  public async Task RunAsync_PreflightClean_ProceedsToRenderAndSend()
  {
    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => session.SendJobAsync(
            A<PrintPayload>.That.Matches(payload => payload.RenderedText.Contains("BON 042", StringComparison.Ordinal)),
            A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RunAsync_TheSameItemTwice_PrintsOneLineWithTheCount()
  {
    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => session.SendJobAsync(
            A<PrintPayload>.That.Matches(payload =>
                payload.RenderedText.Contains("2 x Bratwurst", StringComparison.Ordinal)),
            A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RunAsync_RendersBeforeAllocatingProcessId_ProcessIdNullUntilStep6()
  {
    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => dataAccess.AllocatePrinterJobIdAsync(A<CancellationToken>._)).MustHaveHappened()
        .Then(A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustHaveHappened());
  }


  [Test]
  public async Task RunAsync_SendsPayloadThenWaitsForEchoUpToJobTimeout()
  {
    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => session.SendJobAsync(A<PrintPayload>.That.Matches(payload => payload.PrinterJobId == 17), A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
  }


  [Test]
  public async Task RunAsync_PushesThePrintJobStatusAndTheOrderStatusItWasReadAs()
  {
    A.CallTo(() => dataAccess.LoadOrderPrintJobStatusesAsync(orderId, A<CancellationToken>._))
        .Returns(Task.FromResult(new OrderPrintJobStatuses
        {
          CurrentStatus = OrderStatus.Printed,
          PrintJobStatuses = [PrintJobStatus.Printed],
        }));
    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => callbacks.OnPrintJobStatusChangedAsync(orderId, stationOrderId, PrintJobStatus.Printed, null, A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
    A.CallTo(() => callbacks.OnOrderStatusChangedAsync(orderId, OrderStatus.Printed, A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task Enqueue_JobsAttemptedInPrintJobCreatedAtUtcOrder()
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
    Assert.That(worker.PendingPrintJobIds, Does.Contain(laterTicket));
  }

  [Test]
  public async Task RecoverAtStartupAsync_EnqueuesQueuedAndBlockedTicketsOfAnyEventSession_OldestFirst()
  {
    Guid first = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    Guid second = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    A.CallTo(() => dataAccess.LoadRecoverablePrintJobIdsAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
        .Returns(Task.FromResult<IReadOnlyList<Guid>>([first, second]));

    PrinterWorker worker = Worker();
    await worker.RecoverAtStartupAsync(CancellationToken.None);

    Assert.That(worker.PendingPrintJobIds, Is.EqualTo(new[] { first, second }));
    A.CallTo(() => dataAccess.LoadRecoverablePrintJobIdsAsync(
            A<IReadOnlyCollection<Guid>>.That.Matches(ids => ids.Contains(stationId)),
            A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RecoverAtStartupAsync_MarksPrintingTicketsUnknown()
  {
    PrinterWorker worker = Worker();

    await worker.RecoverAtStartupAsync(CancellationToken.None);

    A.CallTo(() => dataAccess.MarkSendingJobsUnknownAsync(
            A<IReadOnlyCollection<Guid>>.That.Matches(ids => ids.Contains(stationId)),
            A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RunAsync_HeartbeatEvery10Seconds_UsingFakeTimeProvider()
  {
    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);
    await worker.RunOnceAsync(CancellationToken.None);

    timeProvider.Advance(TimeSpan.FromSeconds(11));
    await worker.HeartbeatAsync(CancellationToken.None);

    A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._)).MustHaveHappened();
  }

  [Test]
  public async Task RunAsync_TwoConsecutiveHeartbeatsUnanswered_MarksPrinterOfflineAndForcesReconnect()
  {
    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);
    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._)).Throws(new IOException("no answer"));
    timeProvider.Advance(TimeSpan.FromSeconds(11));
    await worker.HeartbeatAsync(CancellationToken.None);
    timeProvider.Advance(TimeSpan.FromSeconds(11));
    await worker.HeartbeatAsync(CancellationToken.None);

    A.CallTo(() => callbacks.OnPrinterStatusChangedAsync(
            printerId,
            A<IReadOnlyList<Guid>>.That.Matches(ids => ids.Contains(stationId)),
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
    worker.Enqueue(stationOrderId);
    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._)).Returns(Task.FromResult(CleanStatus()));
    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    Assert.That(worker.PendingPrintJobIds, Is.Empty);
  }

  [TestCase(PrintOutcome.Confirmed, 512, PrintJobStatus.Printed, PrintJobStatus.Printed, false)]
  [TestCase(PrintOutcome.Blocked, 0, PrintJobStatus.Blocked, PrintJobStatus.Blocked, true)]
  [TestCase(PrintOutcome.Unreachable, 0, PrintJobStatus.Queued, PrintJobStatus.Queued, true)]
  [TestCase(PrintOutcome.SocketDropped, 0, PrintJobStatus.Queued, PrintJobStatus.Queued, true)]
  [TestCase(PrintOutcome.SocketDropped, 300, PrintJobStatus.Unknown, PrintJobStatus.Unknown, false)]
  [TestCase(PrintOutcome.Timeout, 0, PrintJobStatus.Queued, PrintJobStatus.Queued, true)]
  [TestCase(PrintOutcome.Timeout, 300, PrintJobStatus.Unknown, PrintJobStatus.Unknown, false)]
  [TestCase(PrintOutcome.PrinterError, 0, PrintJobStatus.Blocked, PrintJobStatus.Blocked, true)]
  [TestCase(PrintOutcome.PrinterError, 300, PrintJobStatus.Unknown, PrintJobStatus.Unknown, false)]
  public async Task RunAsync_RetryMapping_ZeroBytesRetried_BytesNeverRetried(
      PrintOutcome outcome,
      int bytesWritten,
      PrintJobStatus expectedJobStatus,
      PrintJobStatus expectedTicketStatus,
      bool expectedRequeue)
  {
    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
        .Returns(Task.FromResult(new PrintDispatchResult(outcome, bytesWritten, CleanStatus(), "mapped")));

    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => dataAccess.ApplyOutcomeAsync(
            A<PrintOutcomeApplication>.That.Matches(application =>
                application.JobStatus == expectedJobStatus),
            A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
    Assert.That(worker.PendingPrintJobIds.Contains(stationOrderId), Is.EqualTo(expectedRequeue));
  }

  [TestCase(PrintOutcome.Blocked)]
  [TestCase(PrintOutcome.Unreachable)]
  public async Task RunAsync_RetryPolicyThrowsForUndefinedRow_MarksTheJobUnknown(PrintOutcome outcome)
  {
    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
        .Returns(Task.FromResult(new PrintDispatchResult(outcome, 300, CleanStatus(), "undefined row")));

    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => dataAccess.ApplyOutcomeAsync(
            A<PrintOutcomeApplication>.That.Matches(application =>
                application.JobStatus == PrintJobStatus.Unknown),
            A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
    Assert.That(worker.PendingPrintJobIds, Does.Not.Contain(stationOrderId));
  }

  [Test]
  public async Task StationCircuitBreaker_TwoConsecutiveUnknownOrTimeoutOutcomes_TripsAtEndpoint()
  {
    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
        .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Timeout, 300, CleanStatus(), "unknown")));
    PrinterWorker worker = Worker(served: [stationId, otherStationId]);
    worker.Enqueue(stationOrderId);
    worker.Enqueue(Guid.Parse("dddddddd-0000-0000-0000-000000000002"));

    await worker.RunOnceAsync(CancellationToken.None);
    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => dataAccess.FailAllWaitingAtEndpointAsync(
            A<IReadOnlyCollection<Guid>>.That.Matches(ids => ids.Contains(stationId) && ids.Contains(otherStationId)),
            PrintFailureReason.StationFaulty,
            A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
    A.CallTo(() => callbacks.OnPrinterStatusChangedAsync(
            printerId,
            A<IReadOnlyList<Guid>>.That.Matches(ids => ids.Contains(stationId) && ids.Contains(otherStationId)),
            A<PrinterStatusSnapshot>._,
            true,
            A<int>._,
            A<CancellationToken>._))
        .MustHaveHappened();
    Assert.That(worker.IsFaulty, Is.True);
  }

  [Test]
  public async Task StationCircuitBreaker_AConfirmedJobResetsTheConsecutiveCounter()
  {
    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
        .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Timeout, 300, CleanStatus(), "unknown")))
        .Once()
        .Then
        .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Confirmed, 300, CleanStatus(), "ok")))
        .Once()
        .Then
        .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Timeout, 300, CleanStatus(), "unknown")));

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
        .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Timeout, 300, CleanStatus(), "unknown")));
    PrinterWorker worker = Worker(served: [stationId, otherStationId]);
    worker.Enqueue(Guid.Parse("ffffffff-0000-0000-0000-000000000001"));
    worker.Enqueue(Guid.Parse("ffffffff-0000-0000-0000-000000000002"));
    await worker.RunOnceAsync(CancellationToken.None);
    await worker.RunOnceAsync(CancellationToken.None);

    IReadOnlyList<Guid> cleared = await worker.ReconnectAsync(CancellationToken.None);

    Assert.That(worker.IsFaulty, Is.False);
    Assert.That(cleared, Is.EquivalentTo(new[] { stationId, otherStationId }));
    A.CallTo(() => dataAccess.ClearFaultyAtEndpointAsync(
            A<IReadOnlyCollection<Guid>>.That.Matches(ids => ids.Contains(stationId) && ids.Contains(otherStationId)),
            A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task GiveUpWindow_MeasuredFromPrintJobCreatedAtUtc_NotFromFirstAttempt()
  {
    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
        .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Unreachable, 0, CleanStatus(), "unreachable")));
    A.CallTo(() => dataAccess.LoadPrintJobAsync(A<Guid>._, A<CancellationToken>._))
        .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Job(id, createdAtUtc: new DateTime(2026, 8, 26, 19, 30, 0, DateTimeKind.Utc))));

    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => dataAccess.FailPrintJobAsync(printJobId, PrintFailureReason.Unreachable, A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
    Assert.That(worker.PendingPrintJobIds, Does.Not.Contain(stationOrderId));
  }

  [Test]
  public async Task GiveUpWindow_SuspendedForFourKnownCauses_ResumesWithoutResettingWhenCauseClears()
  {
    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
        .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Unreachable, 0, CleanStatus(), "unreachable")));
    A.CallTo(() => dataAccess.LoadPrintJobAsync(A<Guid>._, A<CancellationToken>._))
        .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Job(id, createdAtUtc: new DateTime(2026, 8, 26, 19, 30, 0, DateTimeKind.Utc))));
    A.CallTo(() => dataAccess.LoadSuspensionPeriodsAsync(stationId, A<CancellationToken>._))
        .Returns(Task.FromResult<IReadOnlyList<SuspensionPeriod>>(
        [
            new SuspensionPeriod
                {
                    StartedAtUtc = new DateTime(2026, 8, 26, 19, 31, 0, DateTimeKind.Utc),
                    EndedAtUtc = new DateTime(2026, 8, 26, 19, 41, 0, DateTimeKind.Utc),
                },
        ]));

    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => dataAccess.LoadSuspensionPeriodsAsync(stationId, A<CancellationToken>._)).MustHaveHappened();
    Assert.That(worker.PendingPrintJobIds, Does.Contain(stationOrderId));
  }

  [Test]
  public async Task GiveUpWindow_MechanicalErrorBlockedTicket_IsNotSuspendedAndExpiresAtFiveMinutes()
  {
    A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._))
        .Returns(Task.FromResult(new PrinterStatusSnapshot(true, false, false, false, true, "mechanical error", timeProvider.GetUtcNow())));
    A.CallTo(() => dataAccess.LoadPrintJobAsync(A<Guid>._, A<CancellationToken>._))
        .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Job(id, createdAtUtc: new DateTime(2026, 8, 26, 19, 30, 0, DateTimeKind.Utc))));

    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => dataAccess.FailPrintJobAsync(printJobId, PrintFailureReason.PrinterError, A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task OuterBound_TwentyMinutes_FiresUnderEveryKnownCause_NeverFiresOnPrinting()
  {
    A.CallTo(() => dataAccess.LoadPrintJobAsync(A<Guid>._, A<CancellationToken>._))
        .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Job(id, createdAtUtc: new DateTime(2026, 8, 26, 19, 0, 0, DateTimeKind.Utc))));
    A.CallTo(() => dataAccess.LoadSuspensionPeriodsAsync(stationId, A<CancellationToken>._))
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
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => dataAccess.FailPrintJobAsync(printJobId, PrintFailureReason.PaperEnd, A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task StationCircuitBreaker_IsWorkerOwned_NotSharedWithGiveUpWindowCalculator()
  {
    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
        .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Timeout, 300, CleanStatus(), "unknown")));
    A.CallTo(() => dataAccess.LoadPrintJobAsync(A<Guid>._, A<CancellationToken>._))
        .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Job(id, createdAtUtc: timeProvider.GetUtcNow().UtcDateTime)));

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
          PrintJobStatus = PrintJobStatus.HandledOnPaper,
          WasHandledOnPaper = true,
        }));
    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => callbacks.OnPrintJobStatusChangedAsync(
            orderId,
            stationOrderId,
            PrintJobStatus.HandledOnPaper,
            A<PrintFailureReason?>._,
            A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
    Assert.That(worker.PendingPrintJobIds, Does.Not.Contain(stationOrderId));
  }

  [Test]
  public async Task RunAsync_ConnectFails_WaitsTheBackoffBeforeAttemptingToConnectAgain()
  {
    A.CallTo(() => driver.ConnectAsync(A<Printer>._, A<CancellationToken>._))
        .Throws(new PrinterUnreachableException("no route"));
    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);
    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => driver.ConnectAsync(A<Printer>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();

    timeProvider.Advance(TimeSpan.FromMilliseconds(1100));
    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => driver.ConnectAsync(A<Printer>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
  }

  [Test]
  public async Task RunAsync_UnknownOutcome_ReQueriesTheStatusAndPushesItWithoutResending()
  {
    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
        .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Timeout, 300, CleanStatus(), "unknown")));
    PrinterWorker worker = Worker();
    worker.Enqueue(stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => session.QueryStatusAsync(A<CancellationToken>._)).MustHaveHappenedTwiceOrMore();
    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => dataAccess.WritePrinterStatusAsync(printerId, A<PrinterStatusSnapshot>._, A<CancellationToken>._))
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
  }

  [Test]
  public async Task TestPrint_OnAPrinterSeveralStationsUse_CarriesThePrintersOwnName()
  {
    PrinterWorker worker = Worker(served: [stationId, otherStationId]);

    worker.EnqueueTestPrint(Guid.NewGuid());
    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => session.SendJobAsync(
            A<PrintPayload>.That.Matches(payload => payload.StationName == "Drucker Küche"),
            A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task TestPrint_ConnectFails_RecordsAnAttemptRatherThanDroppingTheJob()
  {
    A.CallTo(() => driver.ConnectAsync(A<Printer>._, A<CancellationToken>._))
        .Throws(new PrinterUnreachableException("no route"));
    Guid testJobId = Guid.Parse("14141414-0000-0000-0000-000000000001");
    PrinterWorker worker = Worker();

    worker.EnqueueTestPrint(testJobId);
    await worker.RunOnceAsync(CancellationToken.None);
  }
}
