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

public sealed record RecordedTicketStatus(Guid OrderId, Guid LocationTicketId, LocationTicketStatus Status, PrintFailureReason? FailureReason);

public sealed class RecordingPrintCallbacks : IPrintCallbacks
{
    public List<RecordedTicketStatus> TicketStatusChanges { get; } = [];

    public List<OrderStatus> OrderStatusChanges { get; } = [];

    public List<Guid> FaultyPrinterLocations { get; } = [];

    public int PrinterStatusChangeCount { get; private set; }

    public Task OnTicketStatusChangedAsync(Guid orderId, Guid locationTicketId, LocationTicketStatus newStatus, PrintFailureReason? failureReason, CancellationToken ct)
    {
        TicketStatusChanges.Add(new RecordedTicketStatus(orderId, locationTicketId, newStatus, failureReason));
        return Task.CompletedTask;
    }

    public Task OnOrderStatusChangedAsync(Guid orderId, OrderStatus newStatus, CancellationToken ct)
    {
        OrderStatusChanges.Add(newStatus);
        return Task.CompletedTask;
    }

    public Task OnPrinterStatusChangedAsync(Guid productionLocationId, PrinterStatusSnapshot snapshot, bool isFaulty, int waitingTicketCount, CancellationToken ct)
    {
        PrinterStatusChangeCount++;
        if (isFaulty)
        {
            FaultyPrinterLocations.Add(productionLocationId);
        }

        return Task.CompletedTask;
    }
}

public class PrinterWorkerIntegrationTest
{
    private PrintingSqliteFixture fixture = null!;
    private PrintingSeeder seeder = null!;
    private InMemoryMockFaultRegistry registry = null!;
    private MockPrinterTransport mockTransport = null!;
    private RecordingPrintCallbacks callbacks = null!;
    private EfCorePrinterWorkerDataAccess dataAccess = null!;
    private TestTimeProvider timeProvider = null!;
    private string dataDirectory = null!;
    private Guid kitchenId;
    private Guid barId;

    [SetUp]
    public async Task SetUp()
    {
        kitchenId = Guid.NewGuid();
        barId = Guid.NewGuid();
        dataDirectory = Path.Combine(Path.GetTempPath(), "gastronomy-printing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDirectory);

        fixture = new PrintingSqliteFixture();
        seeder = new PrintingSeeder();
        registry = new InMemoryMockFaultRegistry();
        timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 8, 26, 19, 42, 0, TimeSpan.Zero));
        mockTransport = new MockPrinterTransport(dataDirectory, registry, timeProvider);
        callbacks = new RecordingPrintCallbacks();
        dataAccess = new EfCorePrinterWorkerDataAccess(
            fixture.CreateContext,
            timeProvider);

        await using GastronomyAppDbContext context = fixture.CreateContext();
        await seeder.SeedSessionAsync(context, true, CancellationToken.None);
        await seeder.SeedLocationAsync(context, kitchenId, "Küche", "10.0.0.5", 9100, CancellationToken.None);
        await seeder.SeedLocationAsync(context, barId, "Theke", "10.0.0.5", 9100, CancellationToken.None);
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
        Guid[] locations = served.Length == 0 ? [kitchenId] : served;
        PrinterEndpoint endpoint = new(
            locations[0],
            TransportKind.Mock,
            "10.0.0.5",
            9100,
            null,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(3));

        return new PrinterWorker(
            endpoint,
            locations,
            mockTransport,
            dataAccess,
            callbacks,
            new EscPosSlipRenderer(new ResxSlipTextProvider()),
            new PrinterWorkerDomainServices(
                new RetryPolicy(),
                new GiveUpWindowCalculator(),
                new OrderStatusCalculator(),
                new TicketStateMachine(),
                new PrintJobStateMachine(),
                new PrinterEndpointKeyBuilder()),
            timeProvider,
            new AppLanguage(),
            NullLogger<PrinterWorker>.Instance);
    }

    private async Task<LocationTicketStatus> TicketStatusAsync(Guid ticketId)
    {
        await using GastronomyAppDbContext context = fixture.CreateContext();
        LocationTicket ticket = await context.LocationTickets.SingleAsync(candidate => candidate.Id == ticketId);
        return ticket.Status;
    }

    private string[] SlipFiles(Guid locationId, string name)
    {
        string folder = Path.Combine(dataDirectory, "mock-slips", $"{name}-{locationId.ToString("D")[..8]}");
        return Directory.Exists(folder) ? Directory.GetFiles(folder).Order().ToArray() : [];
    }

    private async Task<SeededTicket> QueueTicketAsync(Guid locationId, int orderNumber, int sequenceNumber, int minutesAfterBaseline)
    {
        await using GastronomyAppDbContext context = fixture.CreateContext();
        SeededTicket seeded = await seeder.SeedOrderAsync(
            context,
            locationId,
            orderNumber,
            sequenceNumber,
            minutesAfterBaseline,
            LocationTicketStatus.Queued,
            CancellationToken.None);

        await dataAccess.CreatePrintJobAsync(seeded.TicketId, locationId, PrintJobKind.Initial, CancellationToken.None);
        return seeded;
    }

    [Test]
    public async Task PaperOutMidEvening_TicketBlocksThenPrintsItselfWhenClearedNoAutoRetryQuestion()
    {
        registry.Arm(kitchenId, MockFault.PaperEnd, MockFaultMode.Sticky);
        SeededTicket seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);
        PrinterWorker worker = Worker();
        worker.Enqueue(seeded.TicketId);

        await worker.RunOnceAsync(CancellationToken.None);

        Assert.That(await TicketStatusAsync(seeded.TicketId), Is.EqualTo(LocationTicketStatus.Blocked));
        Assert.That(SlipFiles(kitchenId, "K_che"), Is.Empty);
        Assert.That(callbacks.PrinterStatusChangeCount, Is.GreaterThan(0));

        registry.Arm(kitchenId, MockFault.None, MockFaultMode.Sticky);
        await worker.RunOnceAsync(CancellationToken.None);

        Assert.That(await TicketStatusAsync(seeded.TicketId), Is.EqualTo(LocationTicketStatus.PrintedOnTestPrinter));
        Assert.That(SlipFiles(kitchenId, "K_che"), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task DropSocketEarly_AutoRetriesWithNoQuestionAskedAndSlipEventuallyPrints()
    {
        registry.Arm(kitchenId, MockFault.DropSocketEarly, MockFaultMode.Once);
        SeededTicket seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);
        PrinterWorker worker = Worker();
        worker.Enqueue(seeded.TicketId);

        await worker.RunOnceAsync(CancellationToken.None);

        Assert.That(await TicketStatusAsync(seeded.TicketId), Is.EqualTo(LocationTicketStatus.Queued));
        Assert.That(worker.PendingTicketIds, Does.Contain(seeded.TicketId));
        Assert.That(callbacks.TicketStatusChanges.Any(change => change.Status == LocationTicketStatus.Unknown), Is.False);

        await worker.RunOnceAsync(CancellationToken.None);

        Assert.That(await TicketStatusAsync(seeded.TicketId), Is.EqualTo(LocationTicketStatus.PrintedOnTestPrinter));
    }

    [Test]
    public async Task BytesWrittenGreaterThanZero_DropsToUnknown_NeverAutoRetried()
    {
        registry.Arm(kitchenId, MockFault.DropSocketMidJob, MockFaultMode.Sticky);
        SeededTicket seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);
        PrinterWorker worker = Worker();
        worker.Enqueue(seeded.TicketId);

        await worker.RunOnceAsync(CancellationToken.None);
        int filesAfterFirstAttempt = SlipFiles(kitchenId, "K_che").Length;
        await worker.RunOnceAsync(CancellationToken.None);
        await worker.RunOnceAsync(CancellationToken.None);

        Assert.That(await TicketStatusAsync(seeded.TicketId), Is.EqualTo(LocationTicketStatus.Unknown));
        Assert.That(worker.PendingTicketIds, Is.Empty);
        Assert.That(SlipFiles(kitchenId, "K_che"), Has.Length.EqualTo(filesAfterFirstAttempt));
    }

    [Test]
    public async Task TwoConsecutiveUnknownOutcomes_TripsBreakerFailsEveryWaitingTicketAtEndpointThenClearsOnReconnect()
    {
        registry.Arm(kitchenId, MockFault.UnknownOutcome, MockFaultMode.Sticky);
        registry.Arm(barId, MockFault.UnknownOutcome, MockFaultMode.Sticky);
        SeededTicket kitchenTicket = await QueueTicketAsync(kitchenId, 137, 42, 0);
        SeededTicket barTicket = await QueueTicketAsync(barId, 138, 11, 1);
        SeededTicket waiting = await QueueTicketAsync(kitchenId, 139, 43, 2);

        PrinterWorker worker = Worker(kitchenId, barId);
        worker.Enqueue(kitchenTicket.TicketId);
        worker.Enqueue(barTicket.TicketId);
        worker.Enqueue(waiting.TicketId);

        await worker.RunOnceAsync(CancellationToken.None);
        await worker.RunOnceAsync(CancellationToken.None);

        Assert.That(worker.IsFaulty, Is.True);
        Assert.That(await TicketStatusAsync(waiting.TicketId), Is.EqualTo(LocationTicketStatus.Failed));

        await using (GastronomyAppDbContext context = fixture.CreateContext())
        {
            List<PrinterStatus> statuses = await context.PrinterStatuses.ToListAsync();
            Assert.That(statuses.All(status => status.IsFaulty), Is.True);
        }

        Assert.That(callbacks.FaultyPrinterLocations, Is.EquivalentTo(new[] { kitchenId, barId }));

        registry.Arm(kitchenId, MockFault.None, MockFaultMode.Sticky);
        registry.Arm(barId, MockFault.None, MockFaultMode.Sticky);
        IReadOnlyList<Guid> cleared = await worker.ReconnectAsync(CancellationToken.None);

        Assert.That(cleared, Is.EquivalentTo(new[] { kitchenId, barId }));
        Assert.That(worker.IsFaulty, Is.False);

        await using (GastronomyAppDbContext context = fixture.CreateContext())
        {
            List<PrinterStatus> statuses = await context.PrinterStatuses.ToListAsync();
            Assert.That(statuses.Any(status => status.IsFaulty), Is.False);
        }

        SeededTicket afterReconnect = await QueueTicketAsync(kitchenId, 140, 44, 3);
        worker.Enqueue(afterReconnect.TicketId);
        await worker.RunOnceAsync(CancellationToken.None);

        Assert.That(await TicketStatusAsync(afterReconnect.TicketId), Is.EqualTo(LocationTicketStatus.PrintedOnTestPrinter));
    }

    [Test]
    public async Task SharedEndpointInterleaving_PreservesPerLocationSequenceOrder()
    {
        SeededTicket kitchenFirst = await QueueTicketAsync(kitchenId, 137, 42, 0);
        SeededTicket barFirst = await QueueTicketAsync(barId, 138, 11, 1);
        SeededTicket kitchenSecond = await QueueTicketAsync(kitchenId, 139, 43, 2);
        SeededTicket barSecond = await QueueTicketAsync(barId, 140, 12, 3);

        PrinterWorker worker = Worker(kitchenId, barId);
        worker.Enqueue(barSecond.TicketId);
        worker.Enqueue(kitchenSecond.TicketId);
        worker.Enqueue(barFirst.TicketId);
        worker.Enqueue(kitchenFirst.TicketId);

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
    public async Task Reprint_ReusesNumbersIncrementsReprintCountPrintsBanner()
    {
        SeededTicket seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);
        PrinterWorker worker = Worker();
        worker.Enqueue(seeded.TicketId);
        await worker.RunOnceAsync(CancellationToken.None);

        await dataAccess.CreatePrintJobAsync(seeded.TicketId, kitchenId, PrintJobKind.Reprint, CancellationToken.None);
        worker.Enqueue(seeded.TicketId);
        await worker.RunOnceAsync(CancellationToken.None);

        await using GastronomyAppDbContext context = fixture.CreateContext();
        LocationTicket ticket = await context.LocationTickets.SingleAsync(candidate => candidate.Id == seeded.TicketId);

        Assert.That(ticket.ReprintCount, Is.EqualTo(1));
        Assert.That(ticket.LocationSequenceNumber, Is.EqualTo(42));

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
    public async Task FailJobOnlyAsync_TicketWasTakenOnPaper_FailsTheJobAndReturnsTheTicketUntouched()
    {
        SeededTicket seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);
        Guid jobId;

        await using (GastronomyAppDbContext context = fixture.CreateContext())
        {
            LocationTicket ticket = await context.LocationTickets.SingleAsync(candidate => candidate.Id == seeded.TicketId);
            ticket.Status = LocationTicketStatus.HandledOnPaper;
            await context.SaveChangesAsync();
            jobId = await context.PrintJobs.Where(job => job.LocationTicketId == seeded.TicketId).Select(job => job.Id).SingleAsync();
        }

        LocationTicketStatus returned = await dataAccess.FailJobOnlyAsync(
            jobId,
            PrintFailureReason.TicketResolvedByHuman,
            CancellationToken.None);

        Assert.That(returned, Is.EqualTo(LocationTicketStatus.HandledOnPaper));
        Assert.That(await TicketStatusAsync(seeded.TicketId), Is.EqualTo(LocationTicketStatus.HandledOnPaper));
    }

    [Test]
    public async Task TicketTakenOnPaperBeforeTheWorkerReachedIt_PrintsNothingAndKeepsHandledOnPaper()
    {
        SeededTicket seeded = await QueueTicketAsync(kitchenId, 137, 42, 0);

        await using (GastronomyAppDbContext context = fixture.CreateContext())
        {
            LocationTicket ticket = await context.LocationTickets.SingleAsync(candidate => candidate.Id == seeded.TicketId);
            ticket.Status = LocationTicketStatus.HandledOnPaper;
            await context.SaveChangesAsync();
        }

        PrinterWorker worker = Worker();
        worker.Enqueue(seeded.TicketId);
        await worker.RunOnceAsync(CancellationToken.None);

        Assert.That(await TicketStatusAsync(seeded.TicketId), Is.EqualTo(LocationTicketStatus.HandledOnPaper));
        Assert.That(SlipFiles(kitchenId, "K_che"), Is.Empty);

        await using GastronomyAppDbContext check = fixture.CreateContext();
        PrintJob job = await check.PrintJobs.SingleAsync(candidate => candidate.LocationTicketId == seeded.TicketId);
        Assert.That(job.Status, Is.EqualTo(PrintJobStatus.Failed));
        Assert.That(job.FailureReason, Is.EqualTo(PrintFailureReason.TicketResolvedByHuman));
        Assert.That(
            callbacks.TicketStatusChanges.Any(change => change.Status == LocationTicketStatus.Failed),
            Is.False,
            "A ticket a station is already cooking from must never be announced as failed.");
    }

    [Test]
    public async Task LoadSuspensionPeriodsAsync_MechanicalErrorOnANetworkPrinter_RunsTheClock()
    {
        await using (GastronomyAppDbContext context = fixture.CreateContext())
        {
            PrinterStatus status = await context.PrinterStatuses.SingleAsync(candidate => candidate.ProductionLocationId == kitchenId);
            status.IsInErrorState = true;
            await context.SaveChangesAsync();
        }

        IReadOnlyList<SuspensionPeriod> network = await dataAccess.LoadSuspensionPeriodsAsync(
            kitchenId,
            TransportKind.Network,
            CancellationToken.None);
        IReadOnlyList<SuspensionPeriod> mock = await dataAccess.LoadSuspensionPeriodsAsync(
            kitchenId,
            TransportKind.Mock,
            CancellationToken.None);

        Assert.That(network, Is.Empty);
        Assert.That(mock, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadSuspensionPeriodsAsync_StationSwitchedOffWithNoStatusRow_StillSuspends()
    {
        Guid quietLocationId = Guid.NewGuid();

        await using (GastronomyAppDbContext context = fixture.CreateContext())
        {
            await seeder.SeedLocationAsync(context, quietLocationId, "Zelt", "10.0.0.9", 9100, CancellationToken.None);
            PrinterStatus status = await context.PrinterStatuses.SingleAsync(candidate => candidate.ProductionLocationId == quietLocationId);
            context.PrinterStatuses.Remove(status);
            PrinterConfiguration configuration = await context.PrinterConfigurations
                .SingleAsync(candidate => candidate.ProductionLocationId == quietLocationId);
            configuration.IsEnabled = false;
            await context.SaveChangesAsync();
        }

        IReadOnlyList<SuspensionPeriod> periods = await dataAccess.LoadSuspensionPeriodsAsync(
            quietLocationId,
            TransportKind.Mock,
            CancellationToken.None);

        Assert.That(periods, Has.Count.EqualTo(1));
        Assert.That(periods[0].EndedAtUtc, Is.Null);
    }
}
