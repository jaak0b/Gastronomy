using GastronomyApp.Api.Printing;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Results;
using GastronomyApp.Infrastructure.Localization;
using GastronomyApp.Infrastructure.Printing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GastronomyApp.Api.Tests.Printing;

public sealed record RecordedPrintJobStatus(Guid OrderId, Guid StationOrderId, PrintJobStatus Status, PrintFailureReason? FailureReason);

public sealed class RecordingPrintCallbacks : IPrintCallbacks
{
  public List<RecordedPrintJobStatus> PrintJobStatusChanges { get; } = [];

  public List<OrderStatus> OrderStatusChanges { get; } = [];

  public List<Guid> FaultyPrinterStations { get; } = [];

  public int PrinterStatusChangeCount { get; private set; }

  public Task OnPrintJobStatusChangedAsync(Guid orderId, Guid stationOrderId, PrintJobStatus newStatus, PrintFailureReason? failureReason, CancellationToken ct)
  {
    PrintJobStatusChanges.Add(new(orderId, stationOrderId, newStatus, failureReason));
    return Task.CompletedTask;
  }

  public Task OnOrderStatusChangedAsync(Guid orderId, OrderStatus newStatus, CancellationToken ct)
  {
    OrderStatusChanges.Add(newStatus);
    return Task.CompletedTask;
  }

  public Task OnPrinterStatusChangedAsync(Guid printerId, IReadOnlyList<Guid> stationIds, PrinterStatusSnapshot snapshot, bool isFaulty, int waitingPrintJobCount, CancellationToken ct)
  {
    PrinterStatusChangeCount++;
    if (isFaulty)
    {
      foreach (var stationId in stationIds)
      {
        FaultyPrinterStations.Add(stationId);
      }
    }

    return Task.CompletedTask;
  }
}

public class PrinterWorkerIntegrationTest
{
  private Guid barId;
  private RecordingPrintCallbacks callbacks = null!;
  private EfCorePrinterWorkerDataAccess dataAccess = null!;
  private string dataDirectory = null!;
  private PrintingSqliteFixture fixture = null!;
  private Guid kitchenId;
  private Guid printerId;
  private InMemoryMockFaultRegistry registry = null!;
  private PrintingSeeder seeder = null!;
  private TestPrinterDriver testPrinterDriver = null!;
  private TestTimeProvider timeProvider = null!;

  [SetUp]
  public async Task SetUp()
  {
    kitchenId = Guid.NewGuid();
    barId = Guid.NewGuid();
    printerId = Guid.NewGuid();
    dataDirectory = Path.Combine(Path.GetTempPath(), "gastronomy-printing-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dataDirectory);

    fixture = new();
    seeder = new();
    registry = new();
    timeProvider = new(new(2026, 8, 26, 19, 42, 0, TimeSpan.Zero));
    testPrinterDriver = new(dataDirectory, registry, timeProvider);
    callbacks = new();
    dataAccess = new(fixture.CreateContext,
                     timeProvider);

    await using var context = fixture.CreateContext();
    await seeder.SeedSessionAsync(context, true, CancellationToken.None);
    await seeder.SeedStationAsync(context, kitchenId, "Küche", printerId, "10.0.0.5", 9100, CancellationToken.None);
    await seeder.SeedStationAsync(context, barId, "Theke", printerId, "10.0.0.5", 9100, CancellationToken.None);
  }

  [TearDown]
  public void TearDown()
  {
    fixture.Dispose();
    if (Directory.Exists(dataDirectory))
    {
      Directory.Delete(dataDirectory, true);
    }
  }

  private PrinterWorker Worker(params Guid[] served)
  {
    Guid[] stations = served.Length == 0 ? [kitchenId] : served;

    return new(new TestPrinter
               {
                 Id = printerId,
                 Name = "Testdrucker"
               },
               stations,
               testPrinterDriver,
               dataAccess,
               callbacks,
               new(new ResxSlipTextProvider()),
               new(new(),
                   new(),
                   new(),
                   new()),
               timeProvider,
               new(),
               NullLogger<PrinterWorker>.Instance);
  }

  private async Task<PrintJobStatus> PrintJobStatusAsync(Guid stationOrderId)
  {
    await using var context = fixture.CreateContext();
    var job = await context.PrintJobs
                           .Where(candidate => candidate.StationOrderId == stationOrderId)
                           .OrderByDescending(candidate => candidate.CopyNumber)
                           .FirstAsync();
    return job.Status;
  }

  private string[] SlipFiles(Guid stationId, string name)
  {
    var folder = Path.Combine(dataDirectory, "mock-slips", $"{name}-{stationId.ToString("D")[..8]}");
    return Directory.Exists(folder) ? Directory.GetFiles(folder).Order().ToArray() : [];
  }

  private async Task<SeededStationOrder> QueueTicketAsync(Guid stationId, int orderNumber, int sequenceNumber, int minutesAfterBaseline)
  {
    await using var context = fixture.CreateContext();
    var seeded = await seeder.SeedOrderAsync(context,
                                             stationId,
                                             orderNumber,
                                             sequenceNumber,
                                             minutesAfterBaseline,
                                             PrintJobStatus.Queued,
                                             CancellationToken.None);

    return seeded;
  }

  [Test]
  public async Task PaperOutMidEvening_TicketBlocksThenPrintsItselfWhenClearedNoAutoRetryQuestion()
  {
    registry.Arm(printerId, MockFault.PaperEnd, MockFaultMode.Sticky);
    var seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);
    var worker = Worker();
    worker.Enqueue(seeded.PrintJobId);

    await worker.RunOnceAsync(CancellationToken.None);

    Assert.That(await PrintJobStatusAsync(seeded.StationOrderId), Is.EqualTo(PrintJobStatus.Blocked));
    Assert.That(SlipFiles(kitchenId, "K_che"), Is.Empty);
    Assert.That(callbacks.PrinterStatusChangeCount, Is.GreaterThan(0));

    registry.Arm(printerId, MockFault.None, MockFaultMode.Sticky);
    await worker.RunOnceAsync(CancellationToken.None);

    Assert.That(await PrintJobStatusAsync(seeded.StationOrderId), Is.EqualTo(PrintJobStatus.Printed));
    Assert.That(SlipFiles(kitchenId, "K_che"), Has.Length.EqualTo(1));
  }

  [Test]
  public async Task DropSocketEarly_AutoRetriesWithNoQuestionAskedAndSlipEventuallyPrints()
  {
    registry.Arm(printerId, MockFault.DropSocketEarly, MockFaultMode.Once);
    var seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);
    var worker = Worker();
    worker.Enqueue(seeded.PrintJobId);

    await worker.RunOnceAsync(CancellationToken.None);

    Assert.That(await PrintJobStatusAsync(seeded.StationOrderId), Is.EqualTo(PrintJobStatus.Queued));
    Assert.That(worker.PendingPrintJobIds, Does.Contain(seeded.PrintJobId));
    Assert.That(callbacks.PrintJobStatusChanges.Any(change => change.Status == PrintJobStatus.Unknown), Is.False);

    await worker.RunOnceAsync(CancellationToken.None);

    Assert.That(await PrintJobStatusAsync(seeded.StationOrderId), Is.EqualTo(PrintJobStatus.Printed));
  }

  [Test]
  public async Task BytesWrittenGreaterThanZero_DropsToUnknown_NeverAutoRetried()
  {
    registry.Arm(printerId, MockFault.DropSocketMidJob, MockFaultMode.Sticky);
    var seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);
    var worker = Worker();
    worker.Enqueue(seeded.PrintJobId);

    await worker.RunOnceAsync(CancellationToken.None);
    var filesAfterFirstAttempt = SlipFiles(kitchenId, "K_che").Length;
    await worker.RunOnceAsync(CancellationToken.None);
    await worker.RunOnceAsync(CancellationToken.None);

    Assert.That(await PrintJobStatusAsync(seeded.StationOrderId), Is.EqualTo(PrintJobStatus.Unknown));
    Assert.That(worker.PendingPrintJobIds, Is.Empty);
    Assert.That(SlipFiles(kitchenId, "K_che"), Has.Length.EqualTo(filesAfterFirstAttempt));
  }

  [Test]
  public async Task TwoConsecutiveUnknownOutcomes_TripsBreakerFailsEveryWaitingTicketAtEndpointThenClearsOnReconnect()
  {
    registry.Arm(printerId, MockFault.UnknownOutcome, MockFaultMode.Sticky);
    registry.Arm(printerId, MockFault.UnknownOutcome, MockFaultMode.Sticky);
    var kitchenTicket = await QueueTicketAsync(kitchenId, 137, 42, 0);
    var barTicket = await QueueTicketAsync(barId, 138, 11, 1);
    var waiting = await QueueTicketAsync(kitchenId, 139, 43, 2);

    var worker = Worker(kitchenId, barId);
    worker.Enqueue(kitchenTicket.PrintJobId);
    worker.Enqueue(barTicket.PrintJobId);
    worker.Enqueue(waiting.PrintJobId);

    await worker.RunOnceAsync(CancellationToken.None);
    await worker.RunOnceAsync(CancellationToken.None);

    Assert.That(worker.IsFaulty, Is.True);
    Assert.That(await PrintJobStatusAsync(waiting.StationOrderId), Is.EqualTo(PrintJobStatus.Failed));

    await using (var context = fixture.CreateContext())
    {
      List<PrinterStatus> statuses = await context.PrinterStatuses.ToListAsync();
      Assert.That(statuses.All(status => status.IsFaulty), Is.True);
    }

    Assert.That(callbacks.FaultyPrinterStations, Is.EquivalentTo(new[] { kitchenId, barId }));

    registry.Arm(printerId, MockFault.None, MockFaultMode.Sticky);
    registry.Arm(printerId, MockFault.None, MockFaultMode.Sticky);
    IReadOnlyList<Guid> cleared = await worker.ReconnectAsync(CancellationToken.None);

    Assert.That(cleared, Is.EquivalentTo(new[] { kitchenId, barId }));
    Assert.That(worker.IsFaulty, Is.False);

    await using (var context = fixture.CreateContext())
    {
      List<PrinterStatus> statuses = await context.PrinterStatuses.ToListAsync();
      Assert.That(statuses.Any(status => status.IsFaulty), Is.False);
    }

    var afterReconnect = await QueueTicketAsync(kitchenId, 140, 44, 3);
    worker.Enqueue(afterReconnect.PrintJobId);
    await worker.RunOnceAsync(CancellationToken.None);

    Assert.That(await PrintJobStatusAsync(afterReconnect.StationOrderId), Is.EqualTo(PrintJobStatus.Printed));
  }

  [Test]
  public async Task SharedEndpointInterleaving_PreservesPerStationSequenceOrder()
  {
    var kitchenFirst = await QueueTicketAsync(kitchenId, 137, 42, 0);
    var barFirst = await QueueTicketAsync(barId, 138, 11, 1);
    var kitchenSecond = await QueueTicketAsync(kitchenId, 139, 43, 2);
    var barSecond = await QueueTicketAsync(barId, 140, 12, 3);

    var worker = Worker(kitchenId, barId);
    worker.Enqueue(barSecond.PrintJobId);
    worker.Enqueue(kitchenSecond.PrintJobId);
    worker.Enqueue(barFirst.PrintJobId);
    worker.Enqueue(kitchenFirst.PrintJobId);

    for (var index = 0; index < 4; index++)
    {
      await worker.RunOnceAsync(CancellationToken.None);
    }

    var kitchenFiles = SlipFiles(kitchenId, "K_che");
    var barFiles = SlipFiles(barId, "Theke");

    Assert.That(kitchenFiles.Select(Path.GetFileName).ToArray(),
                Is.EqualTo(new[]
                           {
                             $"20260826-194200_K_che-{kitchenId.ToString("D")[..8]}_slip-042_print-1.txt",
                             $"20260826-194200_K_che-{kitchenId.ToString("D")[..8]}_slip-043_print-1.txt"
                           }));
    Assert.That(barFiles.Select(Path.GetFileName).ToArray(),
                Is.EqualTo(new[]
                           {
                             $"20260826-194200_Theke-{barId.ToString("D")[..8]}_slip-011_print-1.txt",
                             $"20260826-194200_Theke-{barId.ToString("D")[..8]}_slip-012_print-1.txt"
                           }));
  }

  [Test]
  public async Task AnotherCopy_ReusesTheStationOrderNumberAndPrintsTheCopyBanner()
  {
    var seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);
    var worker = Worker();
    worker.Enqueue(seeded.PrintJobId);
    await worker.RunOnceAsync(CancellationToken.None);

    var copy = await dataAccess.EnsureNextCopyAsync(seeded.StationOrderId, CancellationToken.None);
    worker.Enqueue(copy.PrintJobId!.Value);
    await worker.RunOnceAsync(CancellationToken.None);

    await using var context = fixture.CreateContext();
    var stationOrder = await context.StationOrders.SingleAsync(candidate => candidate.Id == seeded.StationOrderId);
    List<PrintJob> jobs = await context.PrintJobs
                                       .Where(job => job.StationOrderId == seeded.StationOrderId)
                                       .OrderBy(job => job.CopyNumber)
                                       .ToListAsync();

    Assert.That(jobs.Select(job => job.CopyNumber), Is.EqualTo(new[] { 0, 1 }));
    Assert.That(stationOrder.StationOrderNumber, Is.EqualTo(42));

    var files = SlipFiles(kitchenId, "K_che");
    Assert.That(files.Select(Path.GetFileName).ToArray(),
                Is.EqualTo(new[]
                           {
                             $"20260826-194200_K_che-{kitchenId.ToString("D")[..8]}_slip-042_print-1.txt",
                             $"20260826-194200_K_che-{kitchenId.ToString("D")[..8]}_slip-042_print-2.txt"
                           }));
    Assert.That(await File.ReadAllTextAsync(files[1]), Does.Contain("NACHDRUCK"));
    Assert.That(await File.ReadAllTextAsync(files[0]), Does.Not.Contain("NACHDRUCK"));
  }

  [Test]
  public async Task FailPrintJobAsync_TheStationHandledItOnPaper_FailsTheJob()
  {
    var seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);
    Guid jobId;

    await using (var context = fixture.CreateContext())
    {
      var job = await context.PrintJobs.SingleAsync(candidate => candidate.StationOrderId == seeded.StationOrderId);
      job.Status = PrintJobStatus.HandledOnPaper;
      await context.SaveChangesAsync();
      jobId = job.Id;
    }

    await dataAccess.FailPrintJobAsync(jobId, PrintFailureReason.HandledOnPaper, CancellationToken.None);

    Assert.That(await PrintJobStatusAsync(seeded.StationOrderId), Is.EqualTo(PrintJobStatus.HandledOnPaper));
  }

  [Test]
  public async Task StationHandledItOnPaperBeforeTheWorkerReachedIt_PrintsNothingAndKeepsHandledOnPaper()
  {
    var seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);

    await using (var context = fixture.CreateContext())
    {
      var handled = await context.PrintJobs.SingleAsync(candidate => candidate.StationOrderId == seeded.StationOrderId);
      handled.Status = PrintJobStatus.HandledOnPaper;
      await context.SaveChangesAsync();
    }

    var worker = Worker();
    worker.Enqueue(seeded.PrintJobId);
    await worker.RunOnceAsync(CancellationToken.None);

    Assert.That(await PrintJobStatusAsync(seeded.StationOrderId), Is.EqualTo(PrintJobStatus.HandledOnPaper));
    Assert.That(SlipFiles(kitchenId, "K_che"), Is.Empty);

    await using var check = fixture.CreateContext();
    var job = await check.PrintJobs.SingleAsync(candidate => candidate.StationOrderId == seeded.StationOrderId);
    Assert.That(job.Status, Is.EqualTo(PrintJobStatus.HandledOnPaper));
    Assert.That(callbacks.PrintJobStatusChanges.Any(change => change.Status == PrintJobStatus.Failed),
                Is.False,
                "An order a station is already cooking from must never be announced as failed.");
  }

  [Test]
  public async Task LoadSuspensionPeriodsAsync_MechanicalErrorOnANetworkPrinter_RunsTheClock()
  {
    await using (var context = fixture.CreateContext())
    {
      var kitchen = await context.Stations.SingleAsync(candidate => candidate.Id == kitchenId);
      var status = await context.PrinterStatuses
                                .SingleAsync(candidate => candidate.PrinterId == kitchen.PrinterId);
      status.IsInErrorState = true;
      await context.SaveChangesAsync();
    }

    IReadOnlyList<SuspensionPeriod> periods = await dataAccess.LoadSuspensionPeriodsAsync(kitchenId,
                                                                                          CancellationToken.None);

    Assert.That(periods, Has.Count.EqualTo(1));
  }

  [Test]
  public async Task LoadSuspensionPeriodsAsync_StationSwitchedOffWithNoStatusRow_StillSuspends()
  {
    var quietStationId = Guid.NewGuid();

    await using (var context = fixture.CreateContext())
    {
      await seeder.SeedStationAsync(context, quietStationId, "Zelt", Guid.NewGuid(), "10.0.0.9", 9100, CancellationToken.None);
      var station = await context.Stations
                                 .SingleAsync(candidate => candidate.Id == quietStationId);
      var status = await context.PrinterStatuses
                                .SingleAsync(candidate => candidate.PrinterId == station.PrinterId);
      context.PrinterStatuses.Remove(status);
      station.PrinterId = null;
      await context.SaveChangesAsync();
    }

    IReadOnlyList<SuspensionPeriod> periods = await dataAccess.LoadSuspensionPeriodsAsync(quietStationId,
                                                                                          CancellationToken.None);

    Assert.That(periods, Has.Count.EqualTo(1));
    Assert.That(periods[0].EndedAtUtc, Is.Null);
  }
}
