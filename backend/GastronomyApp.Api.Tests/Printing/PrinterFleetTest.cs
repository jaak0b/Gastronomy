using FakeItEasy;
using GastronomyApp.Api.Printing;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Infrastructure.Localization;
using Microsoft.Extensions.Logging.Abstractions;

namespace GastronomyApp.Api.Tests.Printing;

public class PrinterFleetTest
{
  private Guid _anotherPrinterId;
  private Guid _barId;
  private IPrintCallbacks _callbacks = null!;
  private IPrinterWorkerDataAccess _dataAccess = null!;
  private IPrinterDriver _driver = null!;
  private Guid _kitchenId;
  private Guid _onePrinterId;
  private IPrinterSource _printerSource = null!;
  private Guid _printJobId;
  private IPrinterSession _session = null!;
  private Guid _ticketId;
  private TestTimeProvider _timeProvider = null!;

  [SetUp]
  public void SetUp()
  {
    _kitchenId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    _barId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    _ticketId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    _printJobId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    _onePrinterId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    _anotherPrinterId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    _printerSource = A.Fake<IPrinterSource>();
    _dataAccess = A.Fake<IPrinterWorkerDataAccess>();
    _callbacks = A.Fake<IPrintCallbacks>();
    _driver = A.Fake<IPrinterDriver>();
    _session = A.Fake<IPrinterSession>();
    _timeProvider = new(new(2026, 8, 26, 19, 42, 0, TimeSpan.Zero));

    A.CallTo(() => _driver.PrinterType).Returns(typeof(TestPrinter));
    A.CallTo(() => _driver.HeartbeatInterval).Returns(TimeSpan.FromSeconds(10));
    A.CallTo(() => _driver.ConnectAsync(A<Printer>._, A<CancellationToken>._)).Returns(Task.FromResult(_session));
    A.CallTo(() => _dataAccess.LoadRecoverablePrintJobIdsAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<Guid>>([]));
    A.CallTo(() => _dataAccess.ResolveStationAsync(_ticketId, A<CancellationToken>._))
     .Returns(Task.FromResult<Guid?>(_kitchenId));
  }

  private PrinterWithStations Entry(Guid printerId, params Guid[] stationIds)
  {
    return new(new TestPrinter
               {
                 Id = printerId,
                 Name = "Drucker " + printerId.ToString("D")[..4]
               },
               stationIds);
  }

  private PrinterFleet Fleet()
  {
    return new(_printerSource,
               new([_driver]),
               _dataAccess,
               _callbacks,
               new(new ResxSlipTextProvider()),
               new(new(),
                   new(),
                   new(),
                   new()),
               _timeProvider,
               new(),
               NullLoggerFactory.Instance);
  }

  private void Configure(params PrinterWithStations[] entries)
  {
    A.CallTo(() => _printerSource.LoadActiveAsync(A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<PrinterWithStations>>([.. entries]));
  }

  [Test]
  public async Task StartAsync_TwoStationsOnOnePrinter_StartsOneWorkerForBoth()
  {
    Configure(Entry(_onePrinterId, _kitchenId, _barId));
    var fleet = Fleet();

    await fleet.StartAsync(CancellationToken.None);

    Assert.That(fleet.Workers, Has.Count.EqualTo(1));
    Assert.That(fleet.Workers[0].ServedStationIds, Is.EquivalentTo(new[] { _kitchenId, _barId }));
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task StartAsync_TwoStationsOnTwoPrinters_StartsTwoWorkers()
  {
    Configure(Entry(_onePrinterId, _kitchenId), Entry(_anotherPrinterId, _barId));
    var fleet = Fleet();

    await fleet.StartAsync(CancellationToken.None);

    Assert.That(fleet.Workers, Has.Count.EqualTo(2));
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task ReconcileAsync_StationLeavesAPrinter_StopsThatPrintersWorkerWhenTheLastStationLeaves()
  {
    Configure(Entry(_onePrinterId, _kitchenId), Entry(_anotherPrinterId, _barId));
    var fleet = Fleet();
    await fleet.StartAsync(CancellationToken.None);

    Configure(Entry(_onePrinterId, _kitchenId));
    await fleet.ReconcileAsync(CancellationToken.None);

    Assert.That(fleet.Workers, Has.Count.EqualTo(1));
    Assert.That(fleet.Workers[0].ServedStationIds, Is.EqualTo(new[] { _kitchenId }));
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task ReconcileAsync_StationMovedOntoAPrinterThatAlreadyHasAWorker_JoinsThatWorker()
  {
    Configure(Entry(_onePrinterId, _kitchenId));
    var fleet = Fleet();
    await fleet.StartAsync(CancellationToken.None);

    Configure(Entry(_onePrinterId, _kitchenId, _barId));
    await fleet.ReconcileAsync(CancellationToken.None);

    Assert.That(fleet.Workers, Has.Count.EqualTo(1));
    Assert.That(fleet.Workers[0].ServedStationIds, Is.EquivalentTo(new[] { _kitchenId, _barId }));
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task StartAsync_CallsRecoverAtStartupOnEveryWorker()
  {
    Configure(Entry(_onePrinterId, _kitchenId), Entry(_anotherPrinterId, _barId));
    var fleet = Fleet();

    await fleet.StartAsync(CancellationToken.None);

    A.CallTo(() => _dataAccess.MarkSendingJobsUnknownAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
     .MustHaveHappenedTwiceExactly();
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task StopAsync_StopsEveryWorkerCleanly()
  {
    Configure(Entry(_onePrinterId, _kitchenId));
    var fleet = Fleet();
    await fleet.StartAsync(CancellationToken.None);

    await fleet.StopAsync(CancellationToken.None);

    Assert.That(fleet.Workers, Is.Empty);
  }

  [Test]
  public async Task EnqueueAsync_CreatesPrintJobThenEnqueuesOnTheOwningWorker()
  {
    Configure(Entry(_onePrinterId, _kitchenId), Entry(_anotherPrinterId, _barId));
    A.CallTo(() => _dataAccess.EnsureNextCopyAsync(_ticketId, A<CancellationToken>._))
     .Returns(Task.FromResult(new PrintJobEnsured(true, _printJobId, _kitchenId)));
    var fleet = Fleet();
    await fleet.StartAsync(CancellationToken.None);

    await fleet.EnqueueAsync(_ticketId, CancellationToken.None);

    A.CallTo(() => _dataAccess.EnsureNextCopyAsync(_ticketId, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    var owning = fleet.Workers.Single(worker => worker.ServedStationIds.Contains(_kitchenId));
    Assert.That(owning.PendingPrintJobIds, Does.Contain(_printJobId));
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task EnqueueAsync_APrintIsAlreadyRunningForTheStationOrder_ReportsThatNothingWasCreated()
  {
    Configure(Entry(_onePrinterId, _kitchenId));
    A.CallTo(() => _dataAccess.EnsureNextCopyAsync(_ticketId, A<CancellationToken>._))
     .Returns(Task.FromResult(new PrintJobEnsured(false, _printJobId, _kitchenId)));
    var fleet = Fleet();
    await fleet.StartAsync(CancellationToken.None);

    var ensured = await fleet.EnqueueAsync(_ticketId, CancellationToken.None);

    var owning = fleet.Workers.Single(worker => worker.ServedStationIds.Contains(_kitchenId));
    Assert.Multiple(() =>
                    {
                      Assert.That(ensured.WasCreated, Is.False);
                      Assert.That(owning.PendingPrintJobIds, Does.Contain(_printJobId));
                    });
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task EnqueueAsync_UnknownStationOrderId_ThrowsRatherThanSilentlyDroppingTheCall()
  {
    Configure(Entry(_onePrinterId, _kitchenId));
    var unknown = Guid.Parse("99999999-9999-9999-9999-999999999999");
    A.CallTo(() => _dataAccess.EnsureNextCopyAsync(unknown, A<CancellationToken>._))
     .Returns(Task.FromResult(new PrintJobEnsured(false, null, null)));
    var fleet = Fleet();
    await fleet.StartAsync(CancellationToken.None);

    Assert.ThrowsAsync<UnknownStationOrderException>(async () => await fleet.EnqueueAsync(unknown, CancellationToken.None));

    var owning = fleet.Workers.Single(worker => worker.ServedStationIds.Contains(_kitchenId));
    Assert.That(owning.PendingPrintJobIds, Is.Empty);
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task ReconnectAsync_ClearsIsFaultyOnEveryStationOnThatPrinterAndReturnsTheirIds()
  {
    Configure(Entry(_onePrinterId, _kitchenId, _barId));
    var fleet = Fleet();
    await fleet.StartAsync(CancellationToken.None);

    IReadOnlyList<Guid> cleared = await fleet.ReconnectAsync(_onePrinterId, CancellationToken.None);

    Assert.That(cleared, Is.EquivalentTo(new[] { _kitchenId, _barId }));
    A.CallTo(() => _dataAccess.ClearFaultyAtEndpointAsync(A<IReadOnlyCollection<Guid>>.That.Matches(ids => ids.Contains(_kitchenId) && ids.Contains(_barId)),
                                                         A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    await fleet.StopAsync(CancellationToken.None);
  }

  [Test]
  public async Task TestPrintAsync_EnqueuesATestKindJobAtThePrintersOwnWorker()
  {
    Configure(Entry(_onePrinterId, _kitchenId));
    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
     .Returns(Task.FromResult(new PrintDispatchResult(PrintOutcome.Confirmed,
                                                      10,
                                                      new(true, false, false, false, false, "ok", _timeProvider.GetUtcNow()),
                                                      "ok")));
    var fleet = Fleet();
    await fleet.StartAsync(CancellationToken.None);

    await fleet.TestPrintAsync(_onePrinterId, CancellationToken.None);

    var deadline = DateTime.UtcNow.AddSeconds(3);
    while (DateTime.UtcNow < deadline)
    {
      await Task.Delay(20);
    }

    A.CallTo(() => _session.SendJobAsync(A<PrintPayload>.That.Matches(payload => payload.IsTest),
                                        A<CancellationToken>._))
     .MustHaveHappened();
    await fleet.StopAsync(CancellationToken.None);
  }
}
