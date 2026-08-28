using GastronomyApp.Api.Options;
using GastronomyApp.Api.Printing;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
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
        PrintJobStatusChanges.Add(new RecordedPrintJobStatus(orderId, stationOrderId, newStatus, failureReason));
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
            foreach (Guid stationId in stationIds)
            {
                FaultyPrinterStations.Add(stationId);
            }
        }

        return Task.CompletedTask;
    }
}

public class PrinterWorkerIntegrationTest
{
    private PrintingSqliteFixture fixture = null!;
    private PrintingSeeder seeder = null!;
    private InMemoryMockFaultRegistry registry = null!;
    private TestPrinterDriver testPrinterDriver = null!;
    private RecordingPrintCallbacks callbacks = null!;
    private EfCorePrinterWorkerDataAccess dataAccess = null!;
    private TestTimeProvider timeProvider = null!;
    private string dataDirectory = null!;
    private Guid kitchenId;
    private Guid barId;
    private Guid printerId;

    [SetUp]
    public async Task SetUp()
    {
        kitchenId = Guid.NewGuid();
        barId = Guid.NewGuid();
        printerId = Guid.NewGuid();
        dataDirectory = Path.Combine(Path.GetTempPath(), "gastronomy-printing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDirectory);

        fixture = new PrintingSqliteFixture();
        seeder = new PrintingSeeder();
        registry = new InMemoryMockFaultRegistry();
        timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 8, 26, 19, 42, 0, TimeSpan.Zero));
        testPrinterDriver = new TestPrinterDriver(dataDirectory, registry, timeProvider);
        callbacks = new RecordingPrintCallbacks();
        dataAccess = new EfCorePrinterWorkerDataAccess(
            fixture.CreateContext,
            timeProvider);

        await using GastronomyAppDbContext context = fixture.CreateContext();
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

        return new PrinterWorker(
            new TestPrinter
            {
                Id = printerId,
                Name = "Testdrucker",
            },
            stations,
            testPrinterDriver,
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
            NullLogger<PrinterWorker>.Instance);
    }

    private async Task<PrintJobStatus> PrintJobStatusAsync(Guid stationOrderId)
    {
        await using GastronomyAppDbContext context = fixture.CreateContext();
        PrintJob job = await context.PrintJobs
            .Where(candidate => candidate.StationOrderId == stationOrderId)
            .OrderByDescending(candidate => candidate.CopyNumber)
            .FirstAsync();
        return job.Status;
    }

    private string[] SlipFiles(Guid stationId, string name)
    {
        string folder = Path.Combine(dataDirectory, "mock-slips", $"{name}-{stationId.ToString("D")[..8]}");
        return Directory.Exists(folder) ? Directory.GetFiles(folder).Order().ToArray() : [];
    }

    private async Task<SeededStationOrder> QueueTicketAsync(Guid stationId, int orderNumber, int sequenceNumber, int minutesAfterBaseline)
    {
        await using GastronomyAppDbContext context = fixture.CreateContext();
        SeededStationOrder seeded = await seeder.SeedOrderAsync(
            context,
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
        SeededStationOrder seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);
        PrinterWorker worker = Worker();
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
        SeededStationOrder seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);
        PrinterWorker worker = Worker();
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
        SeededStationOrder seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);
        PrinterWorker worker = Worker();
        worker.Enqueue(seeded.PrintJobId);

        await worker.RunOnceAsync(CancellationToken.None);
        int filesAfterFirstAttempt = SlipFiles(kitchenId, "K_che").Length;
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
        SeededStationOrder kitchenTicket = await QueueTicketAsync(kitchenId, 137, 42, 0);
        SeededStationOrder barTicket = await QueueTicketAsync(barId, 138, 11, 1);
        SeededStationOrder waiting = await QueueTicketAsync(kitchenId, 139, 43, 2);

        PrinterWorker worker = Worker(kitchenId, barId);
        worker.Enqueue(kitchenTicket.PrintJobId);
        worker.Enqueue(barTicket.PrintJobId);
        worker.Enqueue(waiting.PrintJobId);

        await worker.RunOnceAsync(CancellationToken.None);
        await worker.RunOnceAsync(CancellationToken.None);

        Assert.That(worker.IsFaulty, Is.True);
        Assert.That(await PrintJobStatusAsync(waiting.StationOrderId), Is.EqualTo(PrintJobStatus.Failed));

        await using (GastronomyAppDbContext context = fixture.CreateContext())
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

        await using (GastronomyAppDbContext context = fixture.CreateContext())
        {
            List<PrinterStatus> statuses = await context.PrinterStatuses.ToListAsync();
            Assert.That(statuses.Any(status => status.IsFaulty), Is.False);
        }

        SeededStationOrder afterReconnect = await QueueTicketAsync(kitchenId, 140, 44, 3);
        worker.Enqueue(afterReconnect.PrintJobId);
        await worker.RunOnceAsync(CancellationToken.None);

        Assert.That(await PrintJobStatusAsync(afterReconnect.StationOrderId), Is.EqualTo(PrintJobStatus.Printed));
    }

    [Test]
    public async Task SharedEndpointInterleaving_PreservesPerStationSequenceOrder()
    {
        SeededStationOrder kitchenFirst = await QueueTicketAsync(kitchenId, 137, 42, 0);
        SeededStationOrder barFirst = await QueueTicketAsync(barId, 138, 11, 1);
        SeededStationOrder kitchenSecond = await QueueTicketAsync(kitchenId, 139, 43, 2);
        SeededStationOrder barSecond = await QueueTicketAsync(barId, 140, 12, 3);

        PrinterWorker worker = Worker(kitchenId, barId);
        worker.Enqueue(barSecond.PrintJobId);
        worker.Enqueue(kitchenSecond.PrintJobId);
        worker.Enqueue(barFirst.PrintJobId);
        worker.Enqueue(kitchenFirst.PrintJobId);

        for (int index = 0; index < 4; index++)
        {
            await worker.RunOnceAsync(CancellationToken.None);
        }

        string[] kitchenFiles = SlipFiles(kitchenId, "K_che");
        string[] barFiles = SlipFiles(barId, "Theke");

        Assert.That(kitchenFiles.Select(Path.GetFileName).ToArray(), Is.EqualTo(new[]
        {
            $"20260826-194200_K_che-{kitchenId.ToString("D")[..8]}_slip-042_print-1.txt",
            $"20260826-194200_K_che-{kitchenId.ToString("D")[..8]}_slip-043_print-1.txt",
        }));
        Assert.That(barFiles.Select(Path.GetFileName).ToArray(), Is.EqualTo(new[]
        {
            $"20260826-194200_Theke-{barId.ToString("D")[..8]}_slip-011_print-1.txt",
            $"20260826-194200_Theke-{barId.ToString("D")[..8]}_slip-012_print-1.txt",
        }));
    }

    [Test]
    public async Task AnotherCopy_ReusesTheStationOrderNumberAndPrintsTheCopyBanner()
    {
        SeededStationOrder seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);
        PrinterWorker worker = Worker();
        worker.Enqueue(seeded.PrintJobId);
        await worker.RunOnceAsync(CancellationToken.None);

        PrintJobEnsured copy = await dataAccess.EnsureNextCopyAsync(seeded.StationOrderId, CancellationToken.None);
        worker.Enqueue(copy.PrintJobId!.Value);
        await worker.RunOnceAsync(CancellationToken.None);

        await using GastronomyAppDbContext context = fixture.CreateContext();
        StationOrder stationOrder = await context.StationOrders.SingleAsync(candidate => candidate.Id == seeded.StationOrderId);
        List<PrintJob> jobs = await context.PrintJobs
            .Where(job => job.StationOrderId == seeded.StationOrderId)
            .OrderBy(job => job.CopyNumber)
            .ToListAsync();

        Assert.That(jobs.Select(job => job.CopyNumber), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(stationOrder.StationOrderNumber, Is.EqualTo(42));

        string[] files = SlipFiles(kitchenId, "K_che");
        Assert.That(files.Select(Path.GetFileName).ToArray(), Is.EqualTo(new[]
        {
            $"20260826-194200_K_che-{kitchenId.ToString("D")[..8]}_slip-042_print-1.txt",
            $"20260826-194200_K_che-{kitchenId.ToString("D")[..8]}_slip-042_print-2.txt",
        }));
        Assert.That(await File.ReadAllTextAsync(files[1]), Does.Contain("NACHDRUCK"));
        Assert.That(await File.ReadAllTextAsync(files[0]), Does.Not.Contain("NACHDRUCK"));
    }

    [Test]
    public async Task FailPrintJobAsync_TheStationHandledItOnPaper_FailsTheJob()
    {
        SeededStationOrder seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);
        Guid jobId;

        await using (GastronomyAppDbContext context = fixture.CreateContext())
        {
            PrintJob job = await context.PrintJobs.SingleAsync(candidate => candidate.StationOrderId == seeded.StationOrderId);
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
        SeededStationOrder seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);

        await using (GastronomyAppDbContext context = fixture.CreateContext())
        {
            PrintJob handled = await context.PrintJobs.SingleAsync(candidate => candidate.StationOrderId == seeded.StationOrderId);
            handled.Status = PrintJobStatus.HandledOnPaper;
            await context.SaveChangesAsync();
        }

        PrinterWorker worker = Worker();
        worker.Enqueue(seeded.PrintJobId);
        await worker.RunOnceAsync(CancellationToken.None);

        Assert.That(await PrintJobStatusAsync(seeded.StationOrderId), Is.EqualTo(PrintJobStatus.HandledOnPaper));
        Assert.That(SlipFiles(kitchenId, "K_che"), Is.Empty);

        await using GastronomyAppDbContext check = fixture.CreateContext();
        PrintJob job = await check.PrintJobs.SingleAsync(candidate => candidate.StationOrderId == seeded.StationOrderId);
        Assert.That(job.Status, Is.EqualTo(PrintJobStatus.HandledOnPaper));
        Assert.That(
            callbacks.PrintJobStatusChanges.Any(change => change.Status == PrintJobStatus.Failed),
            Is.False,
            "An order a station is already cooking from must never be announced as failed.");
    }

    [Test]
    public async Task LoadSuspensionPeriodsAsync_MechanicalErrorOnANetworkPrinter_RunsTheClock()
    {
        await using (GastronomyAppDbContext context = fixture.CreateContext())
        {
            Station kitchen = await context.Stations.SingleAsync(candidate => candidate.Id == kitchenId);
            PrinterStatus status = await context.PrinterStatuses
                .SingleAsync(candidate => candidate.PrinterId == kitchen.PrinterId);
            status.IsInErrorState = true;
            await context.SaveChangesAsync();
        }

        IReadOnlyList<SuspensionPeriod> periods = await dataAccess.LoadSuspensionPeriodsAsync(
            kitchenId,
            CancellationToken.None);

        Assert.That(periods, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadSuspensionPeriodsAsync_StationSwitchedOffWithNoStatusRow_StillSuspends()
    {
        Guid quietStationId = Guid.NewGuid();

        await using (GastronomyAppDbContext context = fixture.CreateContext())
        {
            await seeder.SeedStationAsync(context, quietStationId, "Zelt", Guid.NewGuid(), "10.0.0.9", 9100, CancellationToken.None);
            Station station = await context.Stations
                .SingleAsync(candidate => candidate.Id == quietStationId);
            PrinterStatus status = await context.PrinterStatuses
                .SingleAsync(candidate => candidate.PrinterId == station.PrinterId);
            context.PrinterStatuses.Remove(status);
            station.PrinterId = null;
            await context.SaveChangesAsync();
        }

        IReadOnlyList<SuspensionPeriod> periods = await dataAccess.LoadSuspensionPeriodsAsync(
            quietStationId,
            CancellationToken.None);

        Assert.That(periods, Has.Count.EqualTo(1));
        Assert.That(periods[0].EndedAtUtc, Is.Null);
    }
}
