using System.Diagnostics;
using System.Text;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Infrastructure.Printing;

namespace GastronomyApp.Infrastructure.Tests.Printing;

public class NetworkPrinterTransportTest
{
    private FakeEscPosPrinterServer server = null!;
    private NetworkPrinterTransport transport = null!;
    private Guid stationId;

    [SetUp]
    public async Task SetUp()
    {
        server = new FakeEscPosPrinterServer(0);
        await server.StartAsync(CancellationToken.None);
        transport = new NetworkPrinterTransport(TimeProvider.System);
        stationId = Guid.NewGuid();
    }

    [TearDown]
    public async Task TearDown()
    {
        await server.DisposeAsync();
    }

    private PrinterEndpoint Endpoint(TimeSpan? jobTimeout = null, TimeSpan? heartbeat = null, TimeSpan? statusQueryTimeout = null)
    {
        return new PrinterEndpoint(
            stationId,
            TransportKind.Network,
            "127.0.0.1",
            server.Port,
            null,
            TimeSpan.FromSeconds(2),
            jobTimeout ?? TimeSpan.FromSeconds(2),
            heartbeat ?? TimeSpan.FromSeconds(30),
            statusQueryTimeout ?? TimeSpan.FromMilliseconds(500));
    }

    private PrintPayload Payload(int processId = 7, int sizeInBytes = 64)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(new string('X', sizeInBytes));
        return new PrintPayload(processId, bytes, new string('X', sizeInBytes), PrintJobKind.Initial, 42, 0, stationId, "Kueche");
    }

    private async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout && !condition())
        {
            await Task.Delay(10);
        }
    }

    private bool ContainsSequence(IReadOnlyList<byte> haystack, byte[] needle)
    {
        for (int index = 0; index + needle.Length <= haystack.Count; index++)
        {
            bool match = true;
            for (int offset = 0; offset < needle.Length; offset++)
            {
                if (haystack[index + offset] != needle[offset])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }

    private int CountSequence(IReadOnlyList<byte> haystack, byte[] needle)
    {
        int count = 0;
        for (int index = 0; index + needle.Length <= haystack.Count; index++)
        {
            bool match = true;
            for (int offset = 0; offset < needle.Length; offset++)
            {
                if (haystack[index + offset] != needle[offset])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                count++;
            }
        }

        return count;
    }

    [Test]
    public async Task ConnectAsync_Success_SendsEscAtThenEscT19ThenGsA15()
    {
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        await WaitUntilAsync(() => server.ReceivedBytes.Count >= 8, TimeSpan.FromSeconds(2));

        Assert.That(server.ReceivedBytes.Take(8), Is.EqualTo(new byte[] { 0x1B, 0x40, 0x1B, 0x74, 0x13, 0x1D, 0x61, 0x0F }));
    }

    [Test]
    public async Task ConnectAsync_Success_HoldsOneConnectionAcrossMultipleJobs()
    {
        server.ScriptProcessIdEcho(TimeSpan.Zero);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        await session.SendJobAsync(Payload(1), CancellationToken.None);
        await session.SendJobAsync(Payload(2), CancellationToken.None);

        Assert.That(server.AcceptedConnectionCount, Is.EqualTo(1));
    }

    [Test]
    public async Task SendJobAsync_EchoArrivesBeforeTimeout_ReturnsConfirmedWithFullByteCount()
    {
        server.ScriptProcessIdEcho(TimeSpan.Zero);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);
        PrintPayload payload = Payload();

        PrintDispatchResult result = await session.SendJobAsync(payload, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrintAttemptOutcome.Confirmed));
        Assert.That(result.BytesWritten, Is.EqualTo(payload.Bytes.Length));
    }

    [Test]
    public async Task SendJobAsync_EchoNeverArrives_ReturnsTimeoutAfterJobTimeoutWithBytesWrittenGreaterThanZero()
    {
        server.ScriptNeverEchoProcessId();
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(TimeSpan.FromMilliseconds(200)), CancellationToken.None);

        PrintDispatchResult result = await session.SendJobAsync(Payload(), CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrintAttemptOutcome.Timeout));
        Assert.That(result.BytesWritten, Is.GreaterThan(0));
    }

    [Test]
    public async Task SendJobAsync_ConnectionDropsBeforeFirstByte_ReturnsSocketDroppedWithZeroBytes()
    {
        server.ScriptDropConnectionImmediately();
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);
        await Task.Delay(150);

        PrintDispatchResult result = await session.SendJobAsync(Payload(), CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrintAttemptOutcome.SocketDropped));
        Assert.That(result.BytesWritten, Is.Zero);
    }

    [Test]
    public async Task SendJobAsync_ConnectionDropsMidWrite_ReturnsSocketDroppedWithPartialBytesGreaterThanZero()
    {
        server.ScriptDropConnectionAfterBytes(256);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);
        PrintPayload payload = Payload(sizeInBytes: 200000);

        PrintDispatchResult result = await session.SendJobAsync(payload, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrintAttemptOutcome.SocketDropped));
        Assert.That(result.BytesWritten, Is.GreaterThan(0));
        Assert.That(result.BytesWritten, Is.LessThan(payload.Bytes.Length));
    }

    [Test]
    public async Task SendJobAsync_EchoOnReconnectedSocket_IsDiscarded()
    {
        server.ScriptProcessIdEcho(TimeSpan.Zero);
        await using (IPrinterSession first = await transport.ConnectAsync(Endpoint(TimeSpan.FromMilliseconds(300)), CancellationToken.None))
        {
            await first.SendJobAsync(Payload(11), CancellationToken.None);
        }

        server.ScriptProcessIdEchoCarrying(11);
        await using IPrinterSession second = await transport.ConnectAsync(Endpoint(TimeSpan.FromMilliseconds(300)), CancellationToken.None);
        PrintDispatchResult result = await second.SendJobAsync(Payload(12), CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrintAttemptOutcome.Timeout));
    }

    [Test]
    public async Task QueryStatusAsync_PaperEndMaskSet_ReportsIsPaperEndTrue()
    {
        server.ScriptDleEotResponse(1, 0x16);
        server.ScriptDleEotResponse(2, 0x12);
        server.ScriptDleEotResponse(4, 0x72);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        PrinterStatusSnapshot status = await session.QueryStatusAsync(CancellationToken.None);

        Assert.That(status.IsPaperEnd, Is.True);
    }

    [Test]
    public async Task QueryStatusAsync_CoverOpenBitSet_ReportsIsCoverOpenTrue()
    {
        server.ScriptDleEotResponse(1, 0x16);
        server.ScriptDleEotResponse(2, 0x16);
        server.ScriptDleEotResponse(4, 0x12);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        PrinterStatusSnapshot status = await session.QueryStatusAsync(CancellationToken.None);

        Assert.That(status.IsCoverOpen, Is.True);
        Assert.That(status.IsPaperEnd, Is.False);
    }

    [Test]
    public async Task StatusStream_AsbPushedUnprompted_SurfacesWithoutAQuery()
    {
        server.ScriptAsbOnConnect(0x60, 0x00);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(2));
        PrinterStatusSnapshot? seen = null;
        await foreach (PrinterStatusSnapshot snapshot in session.StatusStream.WithCancellation(cancellation.Token))
        {
            seen = snapshot;
            break;
        }

        Assert.That(seen, Is.Not.Null);
        Assert.That(seen!.IsPaperEnd, Is.True);
    }

    [Test]
    public async Task BytesWritten_AlwaysCountsBytesHandedToSocket_NotBytesAcknowledged()
    {
        server.ScriptNeverEchoProcessId();
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(TimeSpan.FromMilliseconds(200)), CancellationToken.None);
        PrintPayload payload = Payload();

        PrintDispatchResult result = await session.SendJobAsync(payload, CancellationToken.None);

        Assert.That(result.BytesWritten, Is.EqualTo(payload.Bytes.Length));
    }

    [Test]
    public async Task ConnectAsync_HeartbeatEvery10Seconds_KeepsIdleTimeoutAlive()
    {
        await using IPrinterSession session = await transport.ConnectAsync(
            Endpoint(heartbeat: TimeSpan.FromMilliseconds(60)),
            CancellationToken.None);

        await WaitUntilAsync(() => CountSequence(server.ReceivedBytes, [0x10, 0x04, 0x04]) >= 3, TimeSpan.FromSeconds(3));

        Assert.That(CountSequence(server.ReceivedBytes, [0x10, 0x04, 0x04]), Is.GreaterThanOrEqualTo(3));
    }

    [TestCase(150)]
    [TestCase(500)]
    public async Task SendJobAsync_JobTimeoutDefaultsTo90Seconds_MatchesPrinterConfigurationDefault(int jobTimeoutMilliseconds)
    {
        server.ScriptNeverEchoProcessId();
        TimeSpan jobTimeout = TimeSpan.FromMilliseconds(jobTimeoutMilliseconds);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(jobTimeout), CancellationToken.None);

        Stopwatch stopwatch = Stopwatch.StartNew();
        PrintDispatchResult result = await session.SendJobAsync(Payload(), CancellationToken.None);
        stopwatch.Stop();

        Assert.That(result.Outcome, Is.EqualTo(PrintAttemptOutcome.Timeout));
        Assert.That(stopwatch.Elapsed, Is.GreaterThanOrEqualTo(jobTimeout - TimeSpan.FromMilliseconds(50)));
        Assert.That(stopwatch.Elapsed, Is.LessThan(jobTimeout + TimeSpan.FromSeconds(2)));
    }

    [Test]
    public async Task DrainInbound_StatusQueryInFlightWhileEchoAndAsbArrive_DecodesAllThreeWithoutDesynchronising()
    {
        server.ScriptProcessIdEcho(TimeSpan.Zero);
        server.ScriptBurstOnProcessIdRequest(0x60, 0x00, 0x16);
        await using IPrinterSession session = await transport.ConnectAsync(
            Endpoint(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(200)),
            CancellationToken.None);

        Task<PrinterStatusSnapshot> pendingQuery = session.QueryStatusAsync(CancellationToken.None);
        await Task.Delay(50);

        PrintDispatchResult dispatch = await session.SendJobAsync(Payload(21), CancellationToken.None);
        PrinterStatusSnapshot queried = await pendingQuery;

        List<PrinterStatusSnapshot> pushed = [];
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(2));
        try
        {
            await foreach (PrinterStatusSnapshot snapshot in session.StatusStream.WithCancellation(cancellation.Token))
            {
                pushed.Add(snapshot);
                if (pushed.Any(candidate => candidate.IsPaperEnd))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Assert.Fail("The automatic status back block never reached the status stream.");
        }

        Assert.That(dispatch.Outcome, Is.EqualTo(PrintAttemptOutcome.Confirmed));
        Assert.That(pushed.Any(snapshot => snapshot.IsPaperEnd), Is.True);
        Assert.That(queried.IsOnline, Is.True);
        Assert.That(((NetworkPrinterSession)session).UnrecognisedInboundByteCount, Is.Zero);
    }

    [Test]
    public async Task StatusStream_AsbErrorBitSet_ReportsIsInErrorStateUsingTheOfflineStatusErrorBit()
    {
        server.ScriptAsbOnConnect(0x00, 0x40);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(2));
        PrinterStatusSnapshot? seen = null;
        await foreach (PrinterStatusSnapshot snapshot in session.StatusStream.WithCancellation(cancellation.Token))
        {
            seen = snapshot;
            break;
        }

        Assert.That(seen, Is.Not.Null);
        Assert.That(seen!.IsInErrorState, Is.True);
    }

    [Test]
    public async Task StatusStream_AsbPaperFeedBitSet_IsNotReportedAsAnErrorState()
    {
        server.ScriptAsbOnConnect(0x00, 0x08);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(), CancellationToken.None);

        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(2));
        PrinterStatusSnapshot? seen = null;
        await foreach (PrinterStatusSnapshot snapshot in session.StatusStream.WithCancellation(cancellation.Token))
        {
            seen = snapshot;
            break;
        }

        Assert.That(seen, Is.Not.Null);
        Assert.That(seen!.IsInErrorState, Is.False);
    }

    [Test]
    public async Task SendJobAsync_LargePayloadOnAHealthySilentSocket_IsNotAbortedAsSocketDropped()
    {
        server.ScriptProcessIdEcho(TimeSpan.Zero);
        await using IPrinterSession session = await transport.ConnectAsync(Endpoint(TimeSpan.FromSeconds(5)), CancellationToken.None);
        PrintPayload payload = Payload(sizeInBytes: 200000);

        PrintDispatchResult result = await session.SendJobAsync(payload, CancellationToken.None);

        Assert.That(result.Outcome, Is.EqualTo(PrintAttemptOutcome.Confirmed));
        Assert.That(result.BytesWritten, Is.EqualTo(payload.Bytes.Length));
    }

    [TestCase(150)]
    [TestCase(600)]
    public async Task QueryStatusAsync_UnansweredQuery_GivesUpAfterTheEndpointsStatusQueryTimeout(int statusQueryTimeoutMilliseconds)
    {
        TimeSpan statusQueryTimeout = TimeSpan.FromMilliseconds(statusQueryTimeoutMilliseconds);
        await using IPrinterSession session = await transport.ConnectAsync(
            Endpoint(statusQueryTimeout: statusQueryTimeout),
            CancellationToken.None);

        Stopwatch stopwatch = Stopwatch.StartNew();
        await session.QueryStatusAsync(CancellationToken.None);
        stopwatch.Stop();

        TimeSpan expected = statusQueryTimeout + statusQueryTimeout + statusQueryTimeout;
        Assert.That(stopwatch.Elapsed, Is.GreaterThanOrEqualTo(expected - TimeSpan.FromMilliseconds(80)));
        Assert.That(stopwatch.Elapsed, Is.LessThan(expected + TimeSpan.FromSeconds(2)));
    }
}
