using System.Diagnostics;
using System.Text;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Infrastructure.Printing;

namespace GastronomyApp.Infrastructure.Tests.Printing;

public class MockPrinterTransportTest
{
    private string dataDirectory = null!;
    private InMemoryMockFaultRegistry registry = null!;
    private TestTimeProvider timeProvider = null!;
    private MockPrinterTransport transport = null!;
    private Guid locationId;

    [SetUp]
    public void SetUp()
    {
        dataDirectory = Path.Combine(Path.GetTempPath(), "gastronomy-mock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDirectory);
        registry = new InMemoryMockFaultRegistry();
        timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 8, 26, 17, 5, 9, TimeSpan.Zero));
        locationId = Guid.Parse("8f2a1c4b-9d0e-7f6a-3b2c-1d0e9f8a7b6c");
        transport = new MockPrinterTransport(dataDirectory, registry, timeProvider);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, true);
        }
    }

    private PrinterEndpoint Endpoint(Guid? id = null)
    {
        return new PrinterEndpoint(
            id ?? locationId,
            TransportKind.Mock,
            null,
            0,
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(150),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(1));
    }

    private PrintPayload Payload(
        PrintJobKind kind = PrintJobKind.Initial,
        int sequenceNumber = 42,
        int reprintCount = 0,
        int processId = 7,
        string renderedText = "KUECHE\r\nBON 042\r\n",
        Guid? id = null,
        string name = "Küche")
    {
        return new PrintPayload(
            processId,
            Encoding.UTF8.GetBytes(renderedText),
            renderedText,
            kind,
            sequenceNumber,
            reprintCount,
            id ?? locationId,
            name);
    }

    private string LocationFolder(Guid? id = null, string name = "K_che")
    {
        Guid effective = id ?? locationId;
        return Path.Combine(dataDirectory, "mock-slips", $"{name}-{effective.ToString("D")[..8]}");
    }

    private string[] FilesIn(string folder)
    {
        return Directory.Exists(folder) ? Directory.GetFiles(folder) : [];
    }

    [Test]
    public async Task SendJobAsync_NoFault_WritesOneFilePerSlipWithRenderedTextVerbatim()
    {
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);
        PrintPayload payload = Payload();

        PrintDispatchResult result = await session.SendJobAsync(payload, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrintAttemptOutcome.Confirmed));
        Assert.That(result.BytesWritten, Is.EqualTo(payload.Bytes.Length));

        string[] files = FilesIn(LocationFolder());
        Assert.That(files, Has.Length.EqualTo(1));
        Assert.That(Path.GetFileName(files[0]), Is.EqualTo("20260826-170509_K_che-8f2a1c4b_slip-042_print-1.txt"));

        byte[] written = await File.ReadAllBytesAsync(files[0]);
        Assert.That(written, Is.EqualTo(new UTF8Encoding(false).GetBytes(payload.RenderedText)));
    }

    [Test]
    public async Task SendJobAsync_PaperEndArmed_ReturnsBlockedWithZeroBytesAndNoFile()
    {
        registry.Arm(locationId, MockFault.PaperEnd, MockFaultMode.Sticky);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        PrintDispatchResult result = await session.SendJobAsync(Payload(), CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrintAttemptOutcome.Blocked));
        Assert.That(result.BytesWritten, Is.Zero);
        Assert.That(result.StatusAtEnd.IsPaperEnd, Is.True);
        Assert.That(FilesIn(LocationFolder()), Is.Empty);
    }

    [Test]
    public async Task SendJobAsync_CoverOpenArmed_ReturnsBlockedWithZeroBytesAndNoFile()
    {
        registry.Arm(locationId, MockFault.CoverOpen, MockFaultMode.Sticky);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        PrintDispatchResult result = await session.SendJobAsync(Payload(), CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrintAttemptOutcome.Blocked));
        Assert.That(result.BytesWritten, Is.Zero);
        Assert.That(result.StatusAtEnd.IsCoverOpen, Is.True);
        Assert.That(FilesIn(LocationFolder()), Is.Empty);
    }

    [Test]
    public async Task ConnectAsync_ConnectTimeoutArmed_NeverCompletes()
    {
        registry.Arm(locationId, MockFault.ConnectTimeout, MockFaultMode.Sticky);
        using CancellationTokenSource cancellation = new();

        Task<IPrinterSession> connect = transport.ConnectAsync(Endpoint(), cancellation.Token);
        Task completed = await Task.WhenAny(connect, Task.Delay(250));

        Assert.That(completed, Is.Not.SameAs(connect));
        await cancellation.CancelAsync();
    }

    [Test]
    public async Task SendJobAsync_DropSocketEarlyArmed_ReturnsSocketDroppedZeroBytesNoFileWritten()
    {
        registry.Arm(locationId, MockFault.DropSocketEarly, MockFaultMode.Sticky);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        PrintDispatchResult result = await session.SendJobAsync(Payload(), CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrintAttemptOutcome.SocketDropped));
        Assert.That(result.BytesWritten, Is.Zero);
        Assert.That(FilesIn(LocationFolder()), Is.Empty);
    }

    [Test]
    public async Task SendJobAsync_DropSocketMidJobArmed_WritesPartialFileAndReturnsSocketDroppedWithPartialBytes()
    {
        registry.Arm(locationId, MockFault.DropSocketMidJob, MockFaultMode.Sticky);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);
        PrintPayload payload = Payload(renderedText: new string('X', 100));

        PrintDispatchResult result = await session.SendJobAsync(payload, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrintAttemptOutcome.SocketDropped));
        Assert.That(result.BytesWritten, Is.GreaterThan(0));
        Assert.That(result.BytesWritten, Is.LessThan(payload.Bytes.Length));

        string[] files = FilesIn(LocationFolder());
        Assert.That(files, Has.Length.EqualTo(1));
        string content = await File.ReadAllTextAsync(files[0]);
        Assert.That(content, Has.Length.EqualTo(50));
    }

    [Test]
    public async Task SendJobAsync_UnknownOutcomeArmed_AcceptsPayloadWritesNoFileNeverEchoesReturnsTimeout()
    {
        registry.Arm(locationId, MockFault.UnknownOutcome, MockFaultMode.Sticky);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);
        PrintPayload payload = Payload();

        PrintDispatchResult result = await session.SendJobAsync(payload, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrintAttemptOutcome.Timeout));
        Assert.That(result.BytesWritten, Is.EqualTo(payload.Bytes.Length));
        Assert.That(FilesIn(LocationFolder()), Is.Empty);
    }

    [Test]
    public async Task Arm_OnceMode_ClearsAfterOneUseAndSubsequentJobSucceeds()
    {
        registry.Arm(locationId, MockFault.PaperEnd, MockFaultMode.Once);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        PrintDispatchResult first = await session.SendJobAsync(Payload(), CancellationToken.None);
        PrintDispatchResult second = await session.SendJobAsync(Payload(reprintCount: 1), CancellationToken.None);

        Assert.That(first.Outcome, Is.EqualTo(PrintAttemptOutcome.Blocked));
        Assert.That(second.Outcome, Is.EqualTo(PrintAttemptOutcome.Confirmed));
    }

    [Test]
    public async Task Arm_StickyMode_StaysArmedAcrossMultipleJobs()
    {
        registry.Arm(locationId, MockFault.PaperEnd, MockFaultMode.Sticky);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        PrintDispatchResult first = await session.SendJobAsync(Payload(), CancellationToken.None);
        PrintDispatchResult second = await session.SendJobAsync(Payload(reprintCount: 1), CancellationToken.None);

        Assert.That(first.Outcome, Is.EqualTo(PrintAttemptOutcome.Blocked));
        Assert.That(second.Outcome, Is.EqualTo(PrintAttemptOutcome.Blocked));
    }

    [Test]
    public async Task SendJobAsync_ReprintKeepsSequenceNumber_WritesPrintNPlusOneFileBesideOriginal()
    {
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        await session.SendJobAsync(Payload(renderedText: "original"), CancellationToken.None);
        await session.SendJobAsync(Payload(PrintJobKind.Reprint, reprintCount: 1, renderedText: "reprint"), CancellationToken.None);

        string[] files = FilesIn(LocationFolder()).Select(Path.GetFileName).OfType<string>().Order().ToArray();
        Assert.That(files, Is.EqualTo(new[]
        {
            "20260826-170509_K_che-8f2a1c4b_slip-042_print-1.txt",
            "20260826-170509_K_che-8f2a1c4b_slip-042_print-2.txt",
        }));
        Assert.That(await File.ReadAllTextAsync(Path.Combine(LocationFolder(), files[0])), Is.EqualTo("original"));
    }

    [Test]
    public async Task SendJobAsync_TwoSessionsSameFolder_DoNotCollide()
    {
        await using (IPrinterSession first = await transport.ConnectAsync(Endpoint(), CancellationToken.None))
        {
            await first.SendJobAsync(Payload(), CancellationToken.None);
        }

        TestTimeProvider laterClock = new(new DateTimeOffset(2026, 8, 27, 18, 0, 0, TimeSpan.Zero));
        MockPrinterTransport laterTransport = new(dataDirectory, registry, laterClock);
        await using (IPrinterSession second = await laterTransport.ConnectAsync(Endpoint(), CancellationToken.None))
        {
            await second.SendJobAsync(Payload(), CancellationToken.None);
        }

        Assert.That(FilesIn(LocationFolder()), Has.Length.EqualTo(2));
    }

    [Test]
    public async Task SendJobAsync_TwoLocationsSameName_KeptApartByIdSuffix()
    {
        Guid otherId = Guid.Parse("11112222-3333-4444-5555-666677778888");

        await using (IPrinterSession first = await transport.ConnectAsync(Endpoint(), CancellationToken.None))
        {
            await first.SendJobAsync(Payload(name: "Theke"), CancellationToken.None);
        }

        await using (IPrinterSession second = await transport.ConnectAsync(Endpoint(otherId), CancellationToken.None))
        {
            await second.SendJobAsync(Payload(id: otherId, name: "Theke"), CancellationToken.None);
        }

        Assert.That(FilesIn(LocationFolder(name: "Theke")), Has.Length.EqualTo(1));
        Assert.That(FilesIn(LocationFolder(otherId, "Theke")), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task ConnectAsync_UnwritableDataDirectory_ReturnsPrinterErrorZeroBytesAndReportsPathAndReason()
    {
        string blockingFile = Path.Combine(dataDirectory, "blocked");
        await File.WriteAllTextAsync(blockingFile, "not a directory");
        MockPrinterTransport blocked = new(blockingFile, registry, timeProvider);

        await using IPrinterSession session = await blocked.ConnectAsync(Endpoint(), CancellationToken.None);
        PrinterStatusSnapshot status = await session.QueryStatusAsync(CancellationToken.None);
        PrintDispatchResult result = await session.SendJobAsync(Payload(), CancellationToken.None);

        Assert.That(status.IsInErrorState, Is.True);
        Assert.That(status.IsOnline, Is.False);
        Assert.That(status.Detail, Does.Contain(blockingFile));
        Assert.That(status.Detail, Has.Length.GreaterThan(blockingFile.Length));
        Assert.That(result.Outcome, Is.EqualTo(PrintAttemptOutcome.PrinterError));
        Assert.That(result.BytesWritten, Is.Zero);
    }

    [Test]
    public async Task SendJobAsync_TestSlip_FileNameUsesTestPrefixWithProcessId()
    {
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        await session.SendJobAsync(Payload(PrintJobKind.Test, processId: 314), CancellationToken.None);

        string[] files = FilesIn(LocationFolder());
        Assert.That(Path.GetFileName(files[0]), Is.EqualTo("20260826-170509_K_che-8f2a1c4b_test-314.txt"));
    }

    [Test]
    public async Task SendJobAsync_TestSlipWithStationCard_FileContainsBreakGlassUrlAsPlainText()
    {
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);
        string url = "http://192.168.100.123:50000/station/8f2a1c4b9d0e7f6a3b2c1d0e9f8a7b6c";

        await session.SendJobAsync(Payload(PrintJobKind.Test, renderedText: $"TESTBON\r\n{url}\r\n"), CancellationToken.None);

        string content = await File.ReadAllTextAsync(FilesIn(LocationFolder())[0]);
        Assert.That(content, Does.Contain(url));
    }

    [Test]
    public async Task SendJobAsync_HasNoArtificialDelay()
    {
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);
        Stopwatch stopwatch = Stopwatch.StartNew();

        await session.SendJobAsync(Payload(), CancellationToken.None);

        stopwatch.Stop();
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(50));
    }

    [Test]
    public async Task StatusStream_UnknownOutcomeArmed_HoldsJobUntilJobTimeoutSecondsExpires()
    {
        registry.Arm(locationId, MockFault.UnknownOutcome, MockFaultMode.Sticky);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        await session.SendJobAsync(Payload(), CancellationToken.None);

        List<PrinterStatusSnapshot> seen = [];
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(2));
        await foreach (PrinterStatusSnapshot snapshot in session.StatusStream.WithCancellation(cancellation.Token))
        {
            seen.Add(snapshot);
            if (seen.Count == 2)
            {
                break;
            }
        }

        Assert.That(seen, Has.Count.EqualTo(2));
        Assert.That(seen.All(snapshot => snapshot.IsOnline), Is.True);
        Assert.That(seen.Any(snapshot => snapshot.IsInErrorState), Is.False);
    }

    [TestCase(MockFault.None)]
    [TestCase(MockFault.PaperEnd)]
    [TestCase(MockFault.CoverOpen)]
    [TestCase(MockFault.ConnectTimeout)]
    [TestCase(MockFault.DropSocketEarly)]
    [TestCase(MockFault.DropSocketMidJob)]
    [TestCase(MockFault.UnknownOutcome)]
    public void AllSevenFaults_ArmableThroughRegistryInBothOnceAndStickyModes(MockFault fault)
    {
        registry.Arm(locationId, fault, MockFaultMode.Once);
        Assert.That(registry.GetArmedFault(locationId), Is.EqualTo(fault));
        registry.ClearIfOnce(locationId);
        Assert.That(registry.GetArmedFault(locationId), Is.EqualTo(MockFault.None));

        registry.Arm(locationId, fault, MockFaultMode.Sticky);
        Assert.That(registry.GetArmedFault(locationId), Is.EqualTo(fault));
        registry.ClearIfOnce(locationId);
        Assert.That(registry.GetArmedFault(locationId), Is.EqualTo(fault));

        registry.Arm(locationId, MockFault.None, MockFaultMode.Sticky);
        Assert.That(registry.GetArmedFault(locationId), Is.EqualTo(MockFault.None));
    }

    [Test]
    public async Task QueryStatusAsync_OnceModeFaultConsumedAtPreflight_ClearsSoTheNextJobSucceeds()
    {
        registry.Arm(locationId, MockFault.PaperEnd, MockFaultMode.Once);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        PrinterStatusSnapshot preflight = await session.QueryStatusAsync(CancellationToken.None);
        PrintDispatchResult afterPreflight = await session.SendJobAsync(Payload(), CancellationToken.None);

        Assert.That(preflight.IsPaperEnd, Is.True);
        Assert.That(registry.GetArmedFault(locationId), Is.EqualTo(MockFault.None));
        Assert.That(afterPreflight.Outcome, Is.EqualTo(PrintAttemptOutcome.Confirmed));
    }

    [Test]
    public async Task SendJobAsync_UnknownOutcomeArmed_HoldsTheJobForTheEndpointJobTimeout()
    {
        registry.Arm(locationId, MockFault.UnknownOutcome, MockFaultMode.Sticky);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        Stopwatch stopwatch = Stopwatch.StartNew();
        PrintDispatchResult result = await session.SendJobAsync(Payload(), CancellationToken.None);
        stopwatch.Stop();

        Assert.That(result.Outcome, Is.EqualTo(PrintAttemptOutcome.Timeout));
        Assert.That(stopwatch.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(120)));
    }

    [Test]
    public async Task SendJobAsync_DropSocketMidJob_ReportsExactlyTheBytesTheFileReceived()
    {
        registry.Arm(locationId, MockFault.DropSocketMidJob, MockFaultMode.Sticky);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);
        PrintPayload payload = new(
            7,
            new byte[100],
            "0123456789",
            PrintJobKind.Initial,
            42,
            0,
            locationId,
            "Küche");

        PrintDispatchResult result = await session.SendJobAsync(payload, CancellationToken.None);

        long fileLength = new FileInfo(FilesIn(LocationFolder())[0]).Length;
        Assert.That(result.BytesWritten, Is.EqualTo((int)fileLength));
        Assert.That(result.BytesWritten, Is.GreaterThan(0));
    }

    [Test]
    public async Task SendJobAsync_FolderBecomesUnwritableAfterConnect_IsDetectedByTheNextJob()
    {
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);
        PrintDispatchResult first = await session.SendJobAsync(Payload(), CancellationToken.None);

        string slipRoot = Path.Combine(dataDirectory, "mock-slips");
        Directory.Delete(slipRoot, true);
        await File.WriteAllTextAsync(slipRoot, "now a file, not a folder");

        PrintDispatchResult second = await session.SendJobAsync(Payload(reprintCount: 1), CancellationToken.None);

        Assert.That(first.Outcome, Is.EqualTo(PrintAttemptOutcome.Confirmed));
        Assert.That(second.Outcome, Is.EqualTo(PrintAttemptOutcome.PrinterError));
        Assert.That(second.BytesWritten, Is.Zero);
        Assert.That(second.StatusAtEnd.IsInErrorState, Is.True);
    }
}
