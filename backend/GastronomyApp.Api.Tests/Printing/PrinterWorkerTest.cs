using FakeItEasy;
using GastronomyApp.Api.Options;
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

  private readonly AppLanguage _language = new();
  private readonly Guid _printerId = Guid.Parse("7f8e9d0c-1b2a-4c3d-8e5f-6a7b8c9d0e1f");
  private IPrintCallbacks _callbacks = null!;
  private IPrinterWorkerDataAccess _dataAccess = null!;
  private IPrinterDriver _driver = null!;
  private Guid _orderId;
  private Guid _otherStationId;
  private Guid _printJobId;
  private RetryPolicy _retryPolicy = null!;
  private IPrinterSession _session = null!;
  private Guid _stationId;
  private Guid _stationOrderId;
  private TestTimeProvider _timeProvider = null!;

  [SetUp]
  public void SetUp()
  {
    _stationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    _otherStationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    _stationOrderId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    _orderId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    _printJobId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    _dataAccess = A.Fake<IPrinterWorkerDataAccess>();
    _callbacks = A.Fake<IPrintCallbacks>();
    _driver = A.Fake<IPrinterDriver>();
    _session = A.Fake<IPrinterSession>();
    _retryPolicy = new();
    _timeProvider = new(new(2026, 8, 26, 19, 42, 0, TimeSpan.Zero));

    A.CallTo(() => _driver.HeartbeatInterval).Returns(TimeSpan.FromSeconds(10));
    A.CallTo(() => _driver.ConnectTimeout).Returns(TimeSpan.FromSeconds(3));
    A.CallTo(() => _driver.JobTimeout).Returns(TimeSpan.FromSeconds(90));
    A.CallTo(() => _driver.ConnectAsync(A<Printer>._, A<CancellationToken>._)).Returns(Task.FromResult(_session));
    A.CallTo(() => _session.QueryStatusAsync(A<CancellationToken>._)).Returns(Task.FromResult(CleanStatus()));
    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
     .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Confirmed, 512, CleanStatus(), "ok")));
    A.CallTo(() => _dataAccess.LoadPrintJobAsync(A<Guid>._, A<CancellationToken>._))
     .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Job(id)));
    A.CallTo(() => _dataAccess.TryClaimAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.Claimed, PrintJobId = _printJobId }));
    A.CallTo(() => _dataAccess.AllocatePrinterJobIdAsync(A<CancellationToken>._)).Returns(Task.FromResult(17));
    A.CallTo(() => _dataAccess.OrderQueueAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
     .ReturnsLazily((IReadOnlyCollection<Guid> ids, CancellationToken _) => Task.FromResult<IReadOnlyList<Guid>>([.. ids]));
    A.CallTo(() => _dataAccess.LoadOrderPrintJobStatusesAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult(new OrderPrintJobStatuses
                              {
                                CurrentStatus = OrderStatus.Printing,
                                PrintJobStatuses = [PrintJobStatus.Sending]
                              }));
    A.CallTo(() => _dataAccess.LoadSuspensionPeriodsAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<SuspensionPeriod>>([]));
    A.CallTo(() => _dataAccess.ApplyOutcomeAsync(A<PrintOutcomeApplication>._, A<CancellationToken>._))
     .ReturnsLazily((PrintOutcomeApplication application, CancellationToken _) => Task.FromResult(new PrintOutcomeApplied
                                                                                                  {
                                                                                                    PrintJobStatus = application.JobStatus,
                                                                                                    WasHandledOnPaper = false
                                                                                                  }));
    A.CallTo(() => _dataAccess.CountWaitingPrintJobsAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult(0));
  }

  private PrinterStatusSnapshot CleanStatus()
  {
    return new(true, false, false, false, false, "clean", _timeProvider.GetUtcNow());
  }

  private Printer APrinter()
  {
    return new TestPrinter
           {
             Id = _printerId,
             Name = "Drucker Küche"
           };
  }

  private PrintJobLoadResult Job(Guid? id = null,
                                 Guid? station = null,
                                 DateTime? createdAtUtc = null,
                                 int copyNumber = 0,
                                 PrintJobStatus status = PrintJobStatus.Queued)
  {
    return new()
           {
             PrintJobId = id ?? _printJobId,
             StationOrderId = _stationOrderId,
             OrderId = _orderId,
             StationId = station ?? _stationId,
             CopyNumber = copyNumber,
             CreatedAtUtc = createdAtUtc ?? new DateTime(2026, 8, 26, 19, 40, 0, DateTimeKind.Utc),
             Status = status,
             StationOrderNumber = 42,
             StationName = "Küche",
             GlobalOrderNumber = 137,
             TableName = "12",
             StaffMemberName = "Anna",
             OrderNote = null,
             OrderCreatedAtUtc = new(2026, 8, 26, 19, 40, 0, DateTimeKind.Utc),
             Items = [new("Bratwurst", null), new("Bratwurst", null)],
             AlsoGoesToStationNames = []
           };
  }

  private PrinterWorker Worker(IReadOnlyCollection<Guid>? served = null)
  {
    return new(APrinter(),
               served ?? [_stationId],
               _driver,
               _dataAccess,
               _callbacks,
               new(new ResxSlipTextProvider()),
               new(_retryPolicy,
                   new(),
                   new(),
                   new()),
               _timeProvider,
               _language,
               NullLogger<PrinterWorker>.Instance);
  }

  [Test]
  public async Task TestPrint_LaptopSetToEnglish_PrintsTheSlipInEnglish()
  {
    _language.Current = "en";
    PrintPayload? sent = null;
    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
     .Invokes((PrintPayload payload, CancellationToken _) => sent = payload);
    var worker = Worker();

    worker.EnqueueTestPrint(Guid.NewGuid());
    await worker.RunOnceAsync(CancellationToken.None);

    Assert.That(sent!.RenderedText, Does.Contain("TEST SLIP"));
  }

  [Test]
  public async Task TestPrint_LaptopSetToGerman_PrintsTheSlipInGerman()
  {
    _language.Current = "de";
    PrintPayload? sent = null;
    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
     .Invokes((PrintPayload payload, CancellationToken _) => sent = payload);
    var worker = Worker();

    worker.EnqueueTestPrint(Guid.NewGuid());
    await worker.RunOnceAsync(CancellationToken.None);

    Assert.That(sent!.RenderedText, Does.Contain("TESTBON"));
  }

  [Test]
  public async Task RunAsync_ClaimFailsBecauseTheJobIsNoLongerWaiting_TouchesNothingAndReportsTheStatusItFound()
  {
    A.CallTo(() => _dataAccess.LoadPrintJobAsync(A<Guid>._, A<CancellationToken>._))
     .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Job(id, status: PrintJobStatus.HandledOnPaper)));
    A.CallTo(() => _dataAccess.TryClaimAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.NoLongerWaiting, PrintJobId = _printJobId }));
    var worker = Worker();
    worker.Enqueue(_printJobId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _driver.ConnectAsync(A<Printer>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _dataAccess.FailPrintJobAsync(A<Guid>._, A<PrintFailureReason>._, A<CancellationToken>._))
     .MustNotHaveHappened();
    A.CallTo(() => _callbacks.OnPrintJobStatusChangedAsync(_orderId,
                                                          _stationOrderId,
                                                          PrintJobStatus.HandledOnPaper,
                                                          null,
                                                          A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    Assert.That(worker.PendingPrintJobIds, Does.Not.Contain(_printJobId));
  }

  [Test]
  public async Task RunAsync_ClaimSucceeds_MovesTicketToPrintingBeforeSending()
  {
    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _dataAccess.TryClaimAsync(_stationOrderId, A<CancellationToken>._))
     .MustHaveHappened()
     .Then(A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustHaveHappened());
  }

  [Test]
  public async Task RunAsync_PrinterDeclaredFaultySinceEnqueue_LeavesTicketUnclaimedForBreakerToResolve()
  {
    A.CallTo(() => _dataAccess.TryClaimAsync(_stationOrderId, A<CancellationToken>._))
     .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.PrinterFaulty, PrintJobId = _printJobId }));
    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustNotHaveHappened();
    Assert.That(worker.PendingPrintJobIds, Does.Contain(_stationOrderId));
  }

  [Test]
  public async Task RunAsync_NoOpenSession_ConnectsWithConfiguredConnectTimeout()
  {
    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _driver.ConnectAsync(A<Printer>.That.Matches(candidate => candidate.Id == _printerId),
                                       A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RunAsync_ConnectFails_MarksPrinterOfflineLeavesJobQueuedSchedulesReconnectWithBackoff()
  {
    A.CallTo(() => _driver.ConnectAsync(A<Printer>._, A<CancellationToken>._))
     .Throws(new PrinterUnreachableException("no route"));
    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _callbacks.OnPrinterStatusChangedAsync(_printerId,
                                                         A<IReadOnlyList<Guid>>.That.Matches(ids => ids.Contains(_stationId)),
                                                         A<PrinterStatusSnapshot>.That.Matches(snapshot => !snapshot.IsOnline),
                                                         false,
                                                         A<int>._,
                                                         A<CancellationToken>._))
     .MustHaveHappened();
    Assert.That(worker.PendingPrintJobIds, Does.Contain(_stationOrderId));
    Assert.That(new[]
                {
                  worker.NextReconnectDelay(),
                  worker.NextReconnectDelay(),
                  worker.NextReconnectDelay(),
                  worker.NextReconnectDelay(),
                  worker.NextReconnectDelay(),
                  worker.NextReconnectDelay()
                },
                Is.EqualTo(new[]
                           {
                             TimeSpan.FromSeconds(2),
                             TimeSpan.FromSeconds(5),
                             TimeSpan.FromSeconds(10),
                             TimeSpan.FromSeconds(30),
                             TimeSpan.FromSeconds(30),
                             TimeSpan.FromSeconds(30)
                           }));
  }

  [Test]
  public async Task RunAsync_PreflightAsbFresherThanHeartbeatInterval_SkipsQuery()
  {
    var worker = Worker();
    worker.AcceptStatusSnapshot(CleanStatus());
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _session.QueryStatusAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task RunAsync_PreflightStaleAsb_QueriesDleEotN4AndN2()
  {
    var worker = Worker();
    worker.AcceptStatusSnapshot(CleanStatus());
    _timeProvider.Advance(TimeSpan.FromSeconds(30));
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _session.QueryStatusAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RunAsync_PreflightBlocking_MovesToBlockedWithZeroBytesNoSend()
  {
    A.CallTo(() => _session.QueryStatusAsync(A<CancellationToken>._))
     .Returns(Task.FromResult(new PrinterStatusSnapshot(true, true, false, false, false, "paper end", _timeProvider.GetUtcNow())));
    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _dataAccess.ApplyOutcomeAsync(A<PrintOutcomeApplication>.That.Matches(application =>
                                                                                          application.JobStatus == PrintJobStatus.Blocked
                                                                                          && application.BytesWritten == 0),
                                                A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _callbacks.OnPrintJobStatusChangedAsync(_orderId, _stationOrderId, PrintJobStatus.Blocked, A<PrintFailureReason?>._, A<CancellationToken>._))
     .MustHaveHappened();
  }

  [Test]
  public async Task RunAsync_PreflightClean_ProceedsToRenderAndSend()
  {
    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>.That.Matches(payload => payload.RenderedText.Contains("BON 042", StringComparison.Ordinal)),
                                        A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RunAsync_TheSameItemTwice_PrintsOneLineWithTheCount()
  {
    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>.That.Matches(payload =>
                                                                       payload.RenderedText.Contains("2 x Bratwurst", StringComparison.Ordinal)),
                                        A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RunAsync_RendersBeforeAllocatingProcessId_ProcessIdNullUntilStep6()
  {
    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _dataAccess.AllocatePrinterJobIdAsync(A<CancellationToken>._))
     .MustHaveHappened()
     .Then(A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustHaveHappened());
  }


  [Test]
  public async Task RunAsync_SendsPayloadThenWaitsForEchoUpToJobTimeout()
  {
    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>.That.Matches(payload => payload.PrinterJobId == 17), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }


  [Test]
  public async Task RunAsync_PushesThePrintJobStatusAndTheOrderStatusItWasReadAs()
  {
    A.CallTo(() => _dataAccess.LoadOrderPrintJobStatusesAsync(_orderId, A<CancellationToken>._))
     .Returns(Task.FromResult(new OrderPrintJobStatuses
                              {
                                CurrentStatus = OrderStatus.Printed,
                                PrintJobStatuses = [PrintJobStatus.Printed]
                              }));
    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _callbacks.OnPrintJobStatusChangedAsync(_orderId, _stationOrderId, PrintJobStatus.Printed, null, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _callbacks.OnOrderStatusChangedAsync(_orderId, OrderStatus.Printed, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task Enqueue_JobsAttemptedInPrintJobCreatedAtUtcOrder()
  {
    var oldest = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    var middle = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    var newest = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    A.CallTo(() => _dataAccess.OrderQueueAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<Guid>>([oldest, middle, newest]));

    var worker = Worker();
    worker.Enqueue(newest);
    worker.Enqueue(oldest);
    worker.Enqueue(middle);

    List<Guid> attempted = [];
    A.CallTo(() => _dataAccess.TryClaimAsync(A<Guid>._, A<CancellationToken>._))
     .Invokes((Guid id, CancellationToken _) => attempted.Add(id))
     .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.Claimed, PrintJobId = _printJobId }));

    await worker.RunOnceAsync(CancellationToken.None);
    await worker.RunOnceAsync(CancellationToken.None);
    await worker.RunOnceAsync(CancellationToken.None);

    Assert.That(attempted.ToArray(), Is.EqualTo(new[] { oldest, middle, newest }));
  }

  [Test]
  public async Task RunAsync_BlockedJobHoldsStationRatherThanBeingOvertaken()
  {
    var blockedTicket = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    var laterTicket = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    A.CallTo(() => _dataAccess.OrderQueueAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<Guid>>([blockedTicket, laterTicket]));
    A.CallTo(() => _session.QueryStatusAsync(A<CancellationToken>._))
     .Returns(Task.FromResult(new PrinterStatusSnapshot(true, true, false, false, false, "paper end", _timeProvider.GetUtcNow())));

    var worker = Worker();
    worker.Enqueue(blockedTicket);
    worker.Enqueue(laterTicket);

    List<Guid> attempted = [];
    A.CallTo(() => _dataAccess.TryClaimAsync(A<Guid>._, A<CancellationToken>._))
     .Invokes((Guid id, CancellationToken _) => attempted.Add(id))
     .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.Claimed, PrintJobId = _printJobId }));

    await worker.RunOnceAsync(CancellationToken.None);
    await worker.RunOnceAsync(CancellationToken.None);

    Assert.That(attempted.ToArray(), Is.EqualTo(new[] { blockedTicket, blockedTicket }));
    Assert.That(worker.PendingPrintJobIds, Does.Contain(laterTicket));
  }

  [Test]
  public async Task RecoverAtStartupAsync_EnqueuesQueuedAndBlockedTicketsOfAnyEventSession_OldestFirst()
  {
    var first = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    var second = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    A.CallTo(() => _dataAccess.LoadRecoverablePrintJobIdsAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<Guid>>([first, second]));

    var worker = Worker();
    await worker.RecoverAtStartupAsync(CancellationToken.None);

    Assert.That(worker.PendingPrintJobIds, Is.EqualTo(new[] { first, second }));
    A.CallTo(() => _dataAccess.LoadRecoverablePrintJobIdsAsync(A<IReadOnlyCollection<Guid>>.That.Matches(ids => ids.Contains(_stationId)),
                                                              A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RecoverAtStartupAsync_MarksPrintingTicketsUnknown()
  {
    var worker = Worker();

    await worker.RecoverAtStartupAsync(CancellationToken.None);

    A.CallTo(() => _dataAccess.MarkSendingJobsUnknownAsync(A<IReadOnlyCollection<Guid>>.That.Matches(ids => ids.Contains(_stationId)),
                                                          A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RunAsync_HeartbeatEvery10Seconds_UsingFakeTimeProvider()
  {
    var worker = Worker();
    worker.Enqueue(_stationOrderId);
    await worker.RunOnceAsync(CancellationToken.None);

    _timeProvider.Advance(TimeSpan.FromSeconds(11));
    await worker.HeartbeatAsync(CancellationToken.None);

    A.CallTo(() => _session.QueryStatusAsync(A<CancellationToken>._)).MustHaveHappened();
  }

  [Test]
  public async Task RunAsync_TwoConsecutiveHeartbeatsUnanswered_MarksPrinterOfflineAndForcesReconnect()
  {
    var worker = Worker();
    worker.Enqueue(_stationOrderId);
    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _session.QueryStatusAsync(A<CancellationToken>._)).Throws(new IOException("no answer"));
    _timeProvider.Advance(TimeSpan.FromSeconds(11));
    await worker.HeartbeatAsync(CancellationToken.None);
    _timeProvider.Advance(TimeSpan.FromSeconds(11));
    await worker.HeartbeatAsync(CancellationToken.None);

    A.CallTo(() => _callbacks.OnPrinterStatusChangedAsync(_printerId,
                                                         A<IReadOnlyList<Guid>>.That.Matches(ids => ids.Contains(_stationId)),
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
    A.CallTo(() => _session.QueryStatusAsync(A<CancellationToken>._))
     .Returns(Task.FromResult(new PrinterStatusSnapshot(true, true, false, false, false, "paper end", _timeProvider.GetUtcNow())));
    var worker = Worker();
    worker.Enqueue(_stationOrderId);
    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _session.QueryStatusAsync(A<CancellationToken>._)).Returns(Task.FromResult(CleanStatus()));
    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
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
  public async Task RunAsync_RetryMapping_ZeroBytesRetried_BytesNeverRetried(PrintOutcome outcome,
                                                                             int bytesWritten,
                                                                             PrintJobStatus expectedJobStatus,
                                                                             PrintJobStatus expectedTicketStatus,
                                                                             bool expectedRequeue)
  {
    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
     .Returns(Task.FromResult(new PrintDispatchResult(outcome, bytesWritten, CleanStatus(), "mapped")));

    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _dataAccess.ApplyOutcomeAsync(A<PrintOutcomeApplication>.That.Matches(application =>
                                                                                          application.JobStatus == expectedJobStatus),
                                                A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    Assert.That(worker.PendingPrintJobIds.Contains(_stationOrderId), Is.EqualTo(expectedRequeue));
  }

  [TestCase(PrintOutcome.Blocked)]
  [TestCase(PrintOutcome.Unreachable)]
  public async Task RunAsync_RetryPolicyThrowsForUndefinedRow_MarksTheJobUnknown(PrintOutcome outcome)
  {
    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
     .Returns(Task.FromResult(new PrintDispatchResult(outcome, 300, CleanStatus(), "undefined row")));

    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _dataAccess.ApplyOutcomeAsync(A<PrintOutcomeApplication>.That.Matches(application =>
                                                                                          application.JobStatus == PrintJobStatus.Unknown),
                                                A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    Assert.That(worker.PendingPrintJobIds, Does.Not.Contain(_stationOrderId));
  }

  [Test]
  public async Task StationCircuitBreaker_TwoConsecutiveUnknownOrTimeoutOutcomes_TripsAtEndpoint()
  {
    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
     .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Timeout, 300, CleanStatus(), "unknown")));
    var worker = Worker([_stationId, _otherStationId]);
    worker.Enqueue(_stationOrderId);
    worker.Enqueue(Guid.Parse("dddddddd-0000-0000-0000-000000000002"));

    await worker.RunOnceAsync(CancellationToken.None);
    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _dataAccess.FailAllWaitingAtEndpointAsync(A<IReadOnlyCollection<Guid>>.That.Matches(ids => ids.Contains(_stationId) && ids.Contains(_otherStationId)),
                                                            PrintFailureReason.StationFaulty,
                                                            A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _callbacks.OnPrinterStatusChangedAsync(_printerId,
                                                         A<IReadOnlyList<Guid>>.That.Matches(ids => ids.Contains(_stationId) && ids.Contains(_otherStationId)),
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
    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
     .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Timeout, 300, CleanStatus(), "unknown")))
     .Once()
     .Then
     .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Confirmed, 300, CleanStatus(), "ok")))
     .Once()
     .Then
     .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Timeout, 300, CleanStatus(), "unknown")));

    var worker = Worker();
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
    A.CallTo(() => _session.QueryStatusAsync(A<CancellationToken>._))
     .Returns(Task.FromResult(new PrinterStatusSnapshot(true, true, false, false, false, "paper end", _timeProvider.GetUtcNow())));
    var worker = Worker();
    for (var index = 0; index < 50; index++)
    {
      worker.Enqueue(Guid.NewGuid());
    }

    for (var index = 0; index < 50; index++)
    {
      await worker.RunOnceAsync(CancellationToken.None);
    }

    Assert.That(worker.IsFaulty, Is.False);
    A.CallTo(() => _dataAccess.FailAllWaitingAtEndpointAsync(A<IReadOnlyCollection<Guid>>._, A<PrintFailureReason>._, A<CancellationToken>._))
     .MustNotHaveHappened();
  }

  [Test]
  public async Task StationCircuitBreaker_HumanReconnectClearsFaultyAndRestartsWorker()
  {
    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
     .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Timeout, 300, CleanStatus(), "unknown")));
    var worker = Worker([_stationId, _otherStationId]);
    worker.Enqueue(Guid.Parse("ffffffff-0000-0000-0000-000000000001"));
    worker.Enqueue(Guid.Parse("ffffffff-0000-0000-0000-000000000002"));
    await worker.RunOnceAsync(CancellationToken.None);
    await worker.RunOnceAsync(CancellationToken.None);

    IReadOnlyList<Guid> cleared = await worker.ReconnectAsync(CancellationToken.None);

    Assert.That(worker.IsFaulty, Is.False);
    Assert.That(cleared, Is.EquivalentTo(new[] { _stationId, _otherStationId }));
    A.CallTo(() => _dataAccess.ClearFaultyAtEndpointAsync(A<IReadOnlyCollection<Guid>>.That.Matches(ids => ids.Contains(_stationId) && ids.Contains(_otherStationId)),
                                                         A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task GiveUpWindow_MeasuredFromPrintJobCreatedAtUtc_NotFromFirstAttempt()
  {
    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
     .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Unreachable, 0, CleanStatus(), "unreachable")));
    A.CallTo(() => _dataAccess.LoadPrintJobAsync(A<Guid>._, A<CancellationToken>._))
     .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Job(id, createdAtUtc: new DateTime(2026, 8, 26, 19, 30, 0, DateTimeKind.Utc))));

    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _dataAccess.FailPrintJobAsync(_printJobId, PrintFailureReason.Unreachable, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    Assert.That(worker.PendingPrintJobIds, Does.Not.Contain(_stationOrderId));
  }

  [Test]
  public async Task GiveUpWindow_SuspendedForFourKnownCauses_ResumesWithoutResettingWhenCauseClears()
  {
    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
     .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Unreachable, 0, CleanStatus(), "unreachable")));
    A.CallTo(() => _dataAccess.LoadPrintJobAsync(A<Guid>._, A<CancellationToken>._))
     .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Job(id, createdAtUtc: new DateTime(2026, 8, 26, 19, 30, 0, DateTimeKind.Utc))));
    A.CallTo(() => _dataAccess.LoadSuspensionPeriodsAsync(_stationId, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<SuspensionPeriod>>([
                                                                 new()
                                                                 {
                                                                   StartedAtUtc = new(2026, 8, 26, 19, 31, 0, DateTimeKind.Utc),
                                                                   EndedAtUtc = new DateTime(2026, 8, 26, 19, 41, 0, DateTimeKind.Utc)
                                                                 }
                                                               ]));

    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _dataAccess.LoadSuspensionPeriodsAsync(_stationId, A<CancellationToken>._)).MustHaveHappened();
    Assert.That(worker.PendingPrintJobIds, Does.Contain(_stationOrderId));
  }

  [Test]
  public async Task GiveUpWindow_MechanicalErrorBlockedTicket_IsNotSuspendedAndExpiresAtFiveMinutes()
  {
    A.CallTo(() => _session.QueryStatusAsync(A<CancellationToken>._))
     .Returns(Task.FromResult(new PrinterStatusSnapshot(true, false, false, false, true, "mechanical error", _timeProvider.GetUtcNow())));
    A.CallTo(() => _dataAccess.LoadPrintJobAsync(A<Guid>._, A<CancellationToken>._))
     .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Job(id, createdAtUtc: new DateTime(2026, 8, 26, 19, 30, 0, DateTimeKind.Utc))));

    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _dataAccess.FailPrintJobAsync(_printJobId, PrintFailureReason.PrinterError, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task OuterBound_TwentyMinutes_FiresUnderEveryKnownCause_NeverFiresOnPrinting()
  {
    A.CallTo(() => _dataAccess.LoadPrintJobAsync(A<Guid>._, A<CancellationToken>._))
     .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Job(id, createdAtUtc: new DateTime(2026, 8, 26, 19, 0, 0, DateTimeKind.Utc))));
    A.CallTo(() => _dataAccess.LoadSuspensionPeriodsAsync(_stationId, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<SuspensionPeriod>>([
                                                                 new()
                                                                 {
                                                                   StartedAtUtc = new(2026, 8, 26, 19, 0, 0, DateTimeKind.Utc),
                                                                   EndedAtUtc = null
                                                                 }
                                                               ]));
    A.CallTo(() => _session.QueryStatusAsync(A<CancellationToken>._))
     .Returns(Task.FromResult(new PrinterStatusSnapshot(true, true, false, false, false, "paper end", _timeProvider.GetUtcNow())));

    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _dataAccess.FailPrintJobAsync(_printJobId, PrintFailureReason.PaperEnd, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task StationCircuitBreaker_IsWorkerOwned_NotSharedWithGiveUpWindowCalculator()
  {
    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
     .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Timeout, 300, CleanStatus(), "unknown")));
    A.CallTo(() => _dataAccess.LoadPrintJobAsync(A<Guid>._, A<CancellationToken>._))
     .ReturnsLazily((Guid id, CancellationToken _) => Task.FromResult(Job(id, createdAtUtc: _timeProvider.GetUtcNow().UtcDateTime)));

    var worker = Worker();
    worker.Enqueue(Guid.Parse("12121212-0000-0000-0000-000000000001"));
    worker.Enqueue(Guid.Parse("12121212-0000-0000-0000-000000000002"));

    await worker.RunOnceAsync(CancellationToken.None);
    await worker.RunOnceAsync(CancellationToken.None);

    Assert.That(worker.IsFaulty, Is.True);
  }

  [Test]
  public async Task RunAsync_HumanTookTheTicketDuringTheEchoWait_AppliesTheOutcomeToTheJobOnly()
  {
    A.CallTo(() => _dataAccess.ApplyOutcomeAsync(A<PrintOutcomeApplication>._, A<CancellationToken>._))
     .Returns(Task.FromResult(new PrintOutcomeApplied
                              {
                                PrintJobStatus = PrintJobStatus.HandledOnPaper,
                                WasHandledOnPaper = true
                              }));
    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _callbacks.OnPrintJobStatusChangedAsync(_orderId,
                                                          _stationOrderId,
                                                          PrintJobStatus.HandledOnPaper,
                                                          A<PrintFailureReason?>._,
                                                          A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    Assert.That(worker.PendingPrintJobIds, Does.Not.Contain(_stationOrderId));
  }

  [Test]
  public async Task RunAsync_ConnectFails_WaitsTheBackoffBeforeAttemptingToConnectAgain()
  {
    A.CallTo(() => _driver.ConnectAsync(A<Printer>._, A<CancellationToken>._))
     .Throws(new PrinterUnreachableException("no route"));
    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);
    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _driver.ConnectAsync(A<Printer>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();

    _timeProvider.Advance(TimeSpan.FromMilliseconds(1100));
    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _driver.ConnectAsync(A<Printer>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
  }

  [Test]
  public async Task RunAsync_UnknownOutcome_ReQueriesTheStatusAndPushesItWithoutResending()
  {
    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
     .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Timeout, 300, CleanStatus(), "unknown")));
    var worker = Worker();
    worker.Enqueue(_stationOrderId);

    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _session.QueryStatusAsync(A<CancellationToken>._)).MustHaveHappenedTwiceOrMore();
    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _dataAccess.WritePrinterStatusAsync(_printerId, A<PrinterStatusSnapshot>._, A<CancellationToken>._))
     .MustHaveHappenedTwiceOrMore();
  }

  [Test]
  public async Task RunAsync_AttemptNumber_CountsPerJobRatherThanPerWorkerLifetime()
  {
    var firstTicket = Guid.Parse("13131313-0000-0000-0000-000000000001");
    var secondTicket = Guid.Parse("13131313-0000-0000-0000-000000000002");
    var firstJob = Guid.Parse("13131313-1111-0000-0000-000000000001");
    var secondJob = Guid.Parse("13131313-1111-0000-0000-000000000002");

    A.CallTo(() => _dataAccess.TryClaimAsync(firstTicket, A<CancellationToken>._))
     .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.Claimed, PrintJobId = firstJob }));
    A.CallTo(() => _dataAccess.TryClaimAsync(secondTicket, A<CancellationToken>._))
     .Returns(Task.FromResult(new ClaimResult { Outcome = ClaimOutcome.Claimed, PrintJobId = secondJob }));

    var worker = Worker();
    worker.Enqueue(firstTicket);
    worker.Enqueue(secondTicket);

    await worker.RunOnceAsync(CancellationToken.None);
    await worker.RunOnceAsync(CancellationToken.None);
  }

  [Test]
  public async Task TestPrint_OnAPrinterSeveralStationsUse_CarriesThePrintersOwnName()
  {
    var worker = Worker([_stationId, _otherStationId]);

    worker.EnqueueTestPrint(Guid.NewGuid());
    await worker.RunOnceAsync(CancellationToken.None);

    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>.That.Matches(payload => payload.StationName == "Drucker Küche"),
                                        A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task TestPrint_ConnectFails_RecordsAnAttemptRatherThanDroppingTheJob()
  {
    A.CallTo(() => _driver.ConnectAsync(A<Printer>._, A<CancellationToken>._))
     .Throws(new PrinterUnreachableException("no route"));
    var testJobId = Guid.Parse("14141414-0000-0000-0000-000000000001");
    var worker = Worker();

    worker.EnqueueTestPrint(testJobId);
    await worker.RunOnceAsync(CancellationToken.None);
  }
}
