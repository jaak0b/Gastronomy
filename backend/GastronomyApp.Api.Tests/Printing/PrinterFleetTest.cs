using GastronomyApp.Api.Options;
using FakeItEasy;
using GastronomyApp.Api.Printing;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Localization;
using GastronomyApp.Infrastructure.Printing;
using Microsoft.Extensions.Logging.Abstractions;

namespace GastronomyApp.Api.Tests.Printing;

public class PrinterFleetTest
{
  private IPrinterSource printerSource = null!;
  private IPrinterWorkerDataAccess dataAccess = null!;
  private IPrintCallbacks callbacks = null!;
  private IPrinterDriver driver = null!;
  private IPrinterSession session = null!;
  private TestTimeProvider timeProvider = null!;
  private Guid kitchenId;
  private Guid barId;
  private Guid ticketId;
  private Guid printJobId;
  private Guid onePrinterId;
  private Guid anotherPrinterId;

  [SetUp]
  public void SetUp()
  {
    kitchenId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    barId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    ticketId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    printJobId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    onePrinterId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    anotherPrinterId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    printerSource = A.Fake<IPrinterSource>();
    dataAccess = A.Fake<IPrinterWorkerDataAccess>();
    callbacks = A.Fake<IPrintCallbacks>();
    driver = A.Fake<IPrinterDriver>();
    session = A.Fake<IPrinterSession>();
    timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 8, 26, 19, 42, 0, TimeSpan.Zero));

    A.CallTo(() => driver.PrinterType).Returns(typeof(TestPrinter));
    A.CallTo(() => driver.HeartbeatInterval).Returns(TimeSpan.FromSeconds(10));
    A.CallTo(() => driver.ConnectAsync(A<Printer>._, A<CancellationToken>._)).Returns(Task.FromResult(session));
    A.CallTo(() => dataAccess.LoadRecoverablePrintJobIdsAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
        .Returns(Task.FromResult<IReadOnlyList<Guid>>([]));
    A.CallTo(() => dataAccess.ResolveStationAsync(ticketId, A<CancellationToken>._))
        .Returns(Task.FromResult<Guid?>(kitchenId));
  }

  private PrinterWithStations Entry(Guid printerId, params Guid[] stationIds)
  {
    return new PrinterWithStations(
        new TestPrinter
        {
          Id = printerId,
          Name = "Drucker " + printerId.ToString("D")[..4],
        },
        stationIds);
  }

  private PrinterFleet Fleet()
  {
    return new PrinterFleet(
        printerSource,
        new PrinterDriverRegistry([driver]),
        dataAccess,
        callbacks,
        new EscPosSlipRenderer(new ResxSlipTextProvider()),
        new PrinterWorkerDomainServices(
            new RetryPolicy(),
            new GiveUpWindowCalculator(),
            new OrderStatusCalculator(),
            new PrintJobStateMachine()),
        timeProvider,
        new AppLanguage(),
        NullLoggerFactory.Instance);
  }

  private void Configure(params PrinterWithStations[] entries)
  {
    A.CallTo(() => printerSource.LoadActiveAsync(A<CancellationToken>._))
        .Returns(Task.FromResult<IReadOnlyList<PrinterWithStations>>([.. entries]));
  }

  [Test]
  public async Task StartAsync_TwoStationsOnOnePrinter_StartsOneWorkerForBoth()
  {
    Configure(Entry(onePrinterId, kitchenId, barId));
    PrinterFleet fleet = Fleet();

    await fleet.StartAsync(CancellationToken.None);

    Assert.That(fleet.Workers, Has.Count.EqualTo(1));
    Assert.That(fleet.Workers[0].ServedStationIds, Is.EquivalentTo(new[] { kitchenId, barId }));
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task StartAsync_TwoStationsOnTwoPrinters_StartsTwoWorkers()
  {
    Configure(Entry(onePrinterId, kitchenId), Entry(anotherPrinterId, barId));
    PrinterFleet fleet = Fleet();

    await fleet.StartAsync(CancellationToken.None);

    Assert.That(fleet.Workers, Has.Count.EqualTo(2));
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task ReconcileAsync_StationLeavesAPrinter_StopsThatPrintersWorkerWhenTheLastStationLeaves()
  {
    Configure(Entry(onePrinterId, kitchenId), Entry(anotherPrinterId, barId));
    PrinterFleet fleet = Fleet();
    await fleet.StartAsync(CancellationToken.None);

    Configure(Entry(onePrinterId, kitchenId));
    await fleet.ReconcileAsync(CancellationToken.None);

    Assert.That(fleet.Workers, Has.Count.EqualTo(1));
    Assert.That(fleet.Workers[0].ServedStationIds, Is.EqualTo(new[] { kitchenId }));
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task ReconcileAsync_StationMovedOntoAPrinterThatAlreadyHasAWorker_JoinsThatWorker()
  {
    Configure(Entry(onePrinterId, kitchenId));
    PrinterFleet fleet = Fleet();
    await fleet.StartAsync(CancellationToken.None);

    Configure(Entry(onePrinterId, kitchenId, barId));
    await fleet.ReconcileAsync(CancellationToken.None);

    Assert.That(fleet.Workers, Has.Count.EqualTo(1));
    Assert.That(fleet.Workers[0].ServedStationIds, Is.EquivalentTo(new[] { kitchenId, barId }));
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task StartAsync_CallsRecoverAtStartupOnEveryWorker()
  {
    Configure(Entry(onePrinterId, kitchenId), Entry(anotherPrinterId, barId));
    PrinterFleet fleet = Fleet();

    await fleet.StartAsync(CancellationToken.None);

    A.CallTo(() => dataAccess.MarkSendingJobsUnknownAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
        .MustHaveHappenedTwiceExactly();
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task StopAsync_StopsEveryWorkerCleanly()
  {
    Configure(Entry(onePrinterId, kitchenId));
    PrinterFleet fleet = Fleet();
    await fleet.StartAsync(CancellationToken.None);

    await fleet.StopAsync(CancellationToken.None);

    Assert.That(fleet.Workers, Is.Empty);
  }

  [Test]
  public async Task EnqueueAsync_CreatesPrintJobThenEnqueuesOnTheOwningWorker()
  {
    Configure(Entry(onePrinterId, kitchenId), Entry(anotherPrinterId, barId));
    A.CallTo(() => dataAccess.EnsureNextCopyAsync(ticketId, A<CancellationToken>._))
        .Returns(Task.FromResult(new PrintJobEnsured(true, printJobId, kitchenId)));
    PrinterFleet fleet = Fleet();
    await fleet.StartAsync(CancellationToken.None);

    await fleet.EnqueueAsync(ticketId, CancellationToken.None);

    A.CallTo(() => dataAccess.EnsureNextCopyAsync(ticketId, A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
    PrinterWorker owning = fleet.Workers.Single(worker => worker.ServedStationIds.Contains(kitchenId));
    Assert.That(owning.PendingPrintJobIds, Does.Contain(printJobId));
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task EnqueueAsync_APrintIsAlreadyRunningForTheStationOrder_ReportsThatNothingWasCreated()
  {
    Configure(Entry(onePrinterId, kitchenId));
    A.CallTo(() => dataAccess.EnsureNextCopyAsync(ticketId, A<CancellationToken>._))
        .Returns(Task.FromResult(new PrintJobEnsured(false, printJobId, kitchenId)));
    PrinterFleet fleet = Fleet();
    await fleet.StartAsync(CancellationToken.None);

    PrintJobEnsured ensured = await fleet.EnqueueAsync(ticketId, CancellationToken.None);

    PrinterWorker owning = fleet.Workers.Single(worker => worker.ServedStationIds.Contains(kitchenId));
    Assert.Multiple(() =>
    {
      Assert.That(ensured.WasCreated, Is.False);
      Assert.That(owning.PendingPrintJobIds, Does.Contain(printJobId));
    });
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task EnqueueAsync_UnknownStationOrderId_ThrowsRatherThanSilentlyDroppingTheCall()
  {
    Configure(Entry(onePrinterId, kitchenId));
    Guid unknown = Guid.Parse("99999999-9999-9999-9999-999999999999");
    A.CallTo(() => dataAccess.EnsureNextCopyAsync(unknown, A<CancellationToken>._))
        .Returns(Task.FromResult(new PrintJobEnsured(false, null, null)));
    PrinterFleet fleet = Fleet();
    await fleet.StartAsync(CancellationToken.None);

    Assert.ThrowsAsync<UnknownStationOrderException>(
        async () => await fleet.EnqueueAsync(unknown, CancellationToken.None));

    PrinterWorker owning = fleet.Workers.Single(worker => worker.ServedStationIds.Contains(kitchenId));
    Assert.That(owning.PendingPrintJobIds, Is.Empty);
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task ReconnectAsync_ClearsIsFaultyOnEveryStationOnThatPrinterAndReturnsTheirIds()
  {
    Configure(Entry(onePrinterId, kitchenId, barId));
    PrinterFleet fleet = Fleet();
    await fleet.StartAsync(CancellationToken.None);

    IReadOnlyList<Guid> cleared = await fleet.ReconnectAsync(onePrinterId, CancellationToken.None);

    Assert.That(cleared, Is.EquivalentTo(new[] { kitchenId, barId }));
    A.CallTo(() => dataAccess.ClearFaultyAtEndpointAsync(
            A<IReadOnlyCollection<Guid>>.That.Matches(ids => ids.Contains(kitchenId) && ids.Contains(barId)),
            A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task TestPrintAsync_EnqueuesATestKindJobAtThePrintersOwnWorker()
  {
    Configure(Entry(onePrinterId, kitchenId));
    A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
        .Returns(Task.FromResult(new PrintDispatchResult(
            PrintOutcome.Confirmed,
            10,
            new PrinterStatusSnapshot(true, false, false, false, false, "ok", timeProvider.GetUtcNow()),
            "ok")));
    PrinterFleet fleet = Fleet();
    await fleet.StartAsync(CancellationToken.None);

    await fleet.TestPrintAsync(onePrinterId, CancellationToken.None);

    DateTime deadline = DateTime.UtcNow.AddSeconds(3);
    while (DateTime.UtcNow < deadline)
    {
      await Task.Delay(20);
    }

    A.CallTo(() => session.SendJobAsync(
            A<PrintPayload>.That.Matches(payload => payload.IsTest),
            A<CancellationToken>._))
        .MustHaveHappened();
    await fleet.StopAsync(CancellationToken.None);
  }
}
