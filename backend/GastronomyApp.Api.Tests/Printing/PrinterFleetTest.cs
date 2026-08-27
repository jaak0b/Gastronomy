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
    private IPrinterConfigurationSource configurationSource = null!;
    private IPrinterTransportFactory transportFactory = null!;
    private IPrinterWorkerDataAccess dataAccess = null!;
    private IPrintCallbacks callbacks = null!;
    private IPrinterTransport transport = null!;
    private IPrinterSession session = null!;
    private TestTimeProvider timeProvider = null!;
    private Guid kitchenId;
    private Guid barId;
    private Guid ticketId;
    private Guid printJobId;

    [SetUp]
    public void SetUp()
    {
        kitchenId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        barId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        ticketId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        printJobId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        configurationSource = A.Fake<IPrinterConfigurationSource>();
        transportFactory = A.Fake<IPrinterTransportFactory>();
        dataAccess = A.Fake<IPrinterWorkerDataAccess>();
        callbacks = A.Fake<IPrintCallbacks>();
        transport = A.Fake<IPrinterTransport>();
        session = A.Fake<IPrinterSession>();
        timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 8, 26, 19, 42, 0, TimeSpan.Zero));

        A.CallTo(() => transport.Kind).Returns(TransportKind.Mock);
        A.CallTo(() => transportFactory.Create(A<TransportKind>._)).Returns(transport);
        A.CallTo(() => transport.ConnectAsync(A<PrinterEndpoint>._, A<CancellationToken>._)).Returns(Task.FromResult(session));
        A.CallTo(() => dataAccess.LoadRecoverableTicketIdsAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<Guid>>([]));
        A.CallTo(() => dataAccess.ResolveProductionLocationAsync(ticketId, A<CancellationToken>._))
            .Returns(Task.FromResult<Guid?>(kitchenId));
        A.CallTo(() => dataAccess.CreatePrintJobAsync(A<Guid?>._, A<Guid>._, A<PrintJobKind>._, A<CancellationToken>._))
            .Returns(Task.FromResult(printJobId));
    }

    private PrinterConfigurationEntry Entry(Guid locationId, string name, string host, int port)
    {
        return new PrinterConfigurationEntry(
            new ProductionLocation
            {
                Id = locationId,
                Name = name,
                StationAccessKey = "8f2a1c4b9d0e7f6a3b2c1d0e9f8a7b6c",
                SlipLanguage = "de",
                SortOrder = 1,
                IsActive = true,
            },
            new PrinterConfiguration
            {
                ProductionLocationId = locationId,
                TransportKind = TransportKind.Mock,
                Host = host,
                Port = port,
                AgentIdentifier = null,
                CharactersPerLine = 48,
                CodePageName = "PC858",
                ConnectTimeoutSeconds = 3,
                JobTimeoutSeconds = 90,
                HeartbeatSeconds = 10,
                IsEnabled = true,
            });
    }

    private PrinterFleet Fleet()
    {
        return new PrinterFleet(
            configurationSource,
            transportFactory,
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
            NullLoggerFactory.Instance);
    }

    private void Configure(params PrinterConfigurationEntry[] entries)
    {
        A.CallTo(() => configurationSource.LoadEnabledAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<PrinterConfigurationEntry>>([.. entries]));
    }

    [Test]
    public async Task StartAsync_GroupsLocationsByDistinctEndpoint_StartsOneWorkerPerEndpoint()
    {
        Configure(Entry(kitchenId, "Küche", "10.0.0.5", 9100), Entry(barId, "Theke", "10.0.0.5", 9100));
        PrinterFleet fleet = Fleet();

        await fleet.StartAsync(CancellationToken.None);

        Assert.That(fleet.Workers, Has.Count.EqualTo(1));
        Assert.That(fleet.Workers[0].ServedProductionLocationIds, Is.EquivalentTo(new[] { kitchenId, barId }));
        await fleet.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task StartAsync_TwoLocationsDifferentEndpoints_StartsTwoWorkers()
    {
        Configure(Entry(kitchenId, "Küche", "10.0.0.5", 9100), Entry(barId, "Theke", "10.0.0.6", 9100));
        PrinterFleet fleet = Fleet();

        await fleet.StartAsync(CancellationToken.None);

        Assert.That(fleet.Workers, Has.Count.EqualTo(2));
        await fleet.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task ReconcileAsync_LocationDisabledOrMovedToNewEndpoint_StopsItsOldWorkerWhenLastLocationLeaves()
    {
        Configure(Entry(kitchenId, "Küche", "10.0.0.5", 9100), Entry(barId, "Theke", "10.0.0.6", 9100));
        PrinterFleet fleet = Fleet();
        await fleet.StartAsync(CancellationToken.None);

        Configure(Entry(kitchenId, "Küche", "10.0.0.5", 9100));
        await fleet.ReconcileAsync(CancellationToken.None);

        Assert.That(fleet.Workers, Has.Count.EqualTo(1));
        Assert.That(fleet.Workers[0].ServedProductionLocationIds, Is.EqualTo(new[] { kitchenId }));
        await fleet.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task ReconcileAsync_ConfigurationChangeAddingLocationToExistingEndpoint_JoinsExistingWorkerNotANewOne()
    {
        Configure(Entry(kitchenId, "Küche", "10.0.0.5", 9100));
        PrinterFleet fleet = Fleet();
        await fleet.StartAsync(CancellationToken.None);

        Configure(Entry(kitchenId, "Küche", "10.0.0.5", 9100), Entry(barId, "Theke", "10.0.0.5", 9100));
        await fleet.ReconcileAsync(CancellationToken.None);

        Assert.That(fleet.Workers, Has.Count.EqualTo(1));
        Assert.That(fleet.Workers[0].ServedProductionLocationIds, Is.EquivalentTo(new[] { kitchenId, barId }));
        await fleet.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task StartAsync_CallsRecoverAtStartupOnEveryWorker()
    {
        Configure(Entry(kitchenId, "Küche", "10.0.0.5", 9100), Entry(barId, "Theke", "10.0.0.6", 9100));
        PrinterFleet fleet = Fleet();

        await fleet.StartAsync(CancellationToken.None);

        A.CallTo(() => dataAccess.MarkPrintingTicketsUnknownAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
        await fleet.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task StopAsync_StopsEveryWorkerCleanly()
    {
        Configure(Entry(kitchenId, "Küche", "10.0.0.5", 9100));
        PrinterFleet fleet = Fleet();
        await fleet.StartAsync(CancellationToken.None);

        await fleet.StopAsync(CancellationToken.None);

        Assert.That(fleet.Workers, Is.Empty);
    }

    [Test]
    public async Task EnqueueAsync_CreatesPrintJobThenEnqueuesOnTheOwningWorker()
    {
        Configure(Entry(kitchenId, "Küche", "10.0.0.5", 9100), Entry(barId, "Theke", "10.0.0.6", 9100));
        PrinterFleet fleet = Fleet();
        await fleet.StartAsync(CancellationToken.None);

        await fleet.EnqueueAsync(ticketId, PrintJobKind.Initial, CancellationToken.None);

        A.CallTo(() => dataAccess.CreatePrintJobAsync(ticketId, kitchenId, PrintJobKind.Initial, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        PrinterWorker owning = fleet.Workers.Single(worker => worker.ServedProductionLocationIds.Contains(kitchenId));
        Assert.That(owning.PendingTicketIds, Does.Contain(ticketId));
        await fleet.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task EnqueueAsync_UnknownLocationTicketId_ThrowsRatherThanSilentlyDroppingTheCall()
    {
        Configure(Entry(kitchenId, "Küche", "10.0.0.5", 9100));
        Guid unknown = Guid.Parse("99999999-9999-9999-9999-999999999999");
        A.CallTo(() => dataAccess.ResolveProductionLocationAsync(unknown, A<CancellationToken>._))
            .Returns(Task.FromResult<Guid?>(null));
        PrinterFleet fleet = Fleet();
        await fleet.StartAsync(CancellationToken.None);

        Assert.ThrowsAsync<UnknownLocationTicketException>(
            async () => await fleet.EnqueueAsync(unknown, PrintJobKind.Initial, CancellationToken.None));

        A.CallTo(() => dataAccess.CreatePrintJobAsync(A<Guid?>._, A<Guid>._, A<PrintJobKind>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        await fleet.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task ReconnectAsync_ClearsIsFaultyOnEveryLocationSharingTheEndpointAndReturnsTheirIds()
    {
        Configure(Entry(kitchenId, "Küche", "10.0.0.5", 9100), Entry(barId, "Theke", "10.0.0.5", 9100));
        PrinterFleet fleet = Fleet();
        await fleet.StartAsync(CancellationToken.None);

        IReadOnlyList<Guid> cleared = await fleet.ReconnectAsync(barId, CancellationToken.None);

        Assert.That(cleared, Is.EquivalentTo(new[] { kitchenId, barId }));
        A.CallTo(() => dataAccess.ClearFaultyAtEndpointAsync(
                A<IReadOnlyCollection<Guid>>.That.Matches(ids => ids.Contains(kitchenId) && ids.Contains(barId)),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        await fleet.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task TestPrintAsync_EnqueuesATestKindJobAtTheGivenLocationsWorker()
    {
        Configure(Entry(kitchenId, "Küche", "10.0.0.5", 9100));
        A.CallTo(() => dataAccess.LoadTestPrintAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new TestPrintLoadResult("Küche", "de", new Uri("http://10.0.0.5:5000/station/8f2a1c4b9d0e7f6a3b2c1d0e9f8a7b6c"))));
        A.CallTo(() => session.SendJobAsync(A<PrintPayload>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new PrintDispatchResult(
                PrintAttemptOutcome.Confirmed,
                10,
                new PrinterStatusSnapshot(true, false, false, false, false, "ok", timeProvider.GetUtcNow()),
                "ok")));
        PrinterFleet fleet = Fleet();
        await fleet.StartAsync(CancellationToken.None);

        await fleet.TestPrintAsync(kitchenId, CancellationToken.None);

        A.CallTo(() => dataAccess.CreatePrintJobAsync(null, kitchenId, PrintJobKind.Test, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        DateTime deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        A.CallTo(() => session.SendJobAsync(
                A<PrintPayload>.That.Matches(payload => payload.Kind == PrintJobKind.Test),
                A<CancellationToken>._))
            .MustHaveHappened();
        await fleet.StopAsync(CancellationToken.None);
    }
}
