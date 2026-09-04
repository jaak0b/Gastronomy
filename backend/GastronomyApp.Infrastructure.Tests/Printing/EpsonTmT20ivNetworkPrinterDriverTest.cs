using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Infrastructure.Printing;

namespace GastronomyApp.Infrastructure.Tests.Printing;

public class EpsonTmT20ivNetworkPrinterDriverTest
{
  private FakeEscPosPrinterServer _server = null!;
  private Guid _stationId;

  [SetUp]
  public async Task SetUp()
  {
    _server = new(0);
    await _server.StartAsync(CancellationToken.None);
    _stationId = Guid.NewGuid();
  }

  [TearDown]
  public async Task TearDown()
  {
    await _server.DisposeAsync();
  }

  private async Task<IPrinterSession> OpenAsync(TimeSpan? jobTimeout = null,
                                                TimeSpan? heartbeat = null,
                                                TimeSpan? statusQueryTimeout = null)
  {
    TcpClient client = new();
    await client.ConnectAsync("127.0.0.1", _server.Port, CancellationToken.None);
    EpsonTmT20ivNetworkPrinterSession session = new(client,
                                                    new(jobTimeout ?? TimeSpan.FromSeconds(2),
                                                        heartbeat ?? TimeSpan.FromSeconds(30),
                                                        statusQueryTimeout ?? TimeSpan.FromMilliseconds(500)),
                                                    TimeProvider.System);
    await session.StartAsync();
    return session;
  }

  private PrintPayload Payload(int processId = 7, int sizeInBytes = 64)
  {
    var bytes = Encoding.ASCII.GetBytes(new string('X', sizeInBytes));
    return new(processId, bytes, new('X', sizeInBytes), 0, 42, _stationId, "Kueche", false);
  }

  private async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
  {
    var stopwatch = Stopwatch.StartNew();
    while (stopwatch.Elapsed < timeout && !condition())
    {
      await Task.Delay(10);
    }
  }

  private bool ContainsSequence(IReadOnlyList<byte> haystack, byte[] needle)
  {
    for (var index = 0; index + needle.Length <= haystack.Count; index++)
    {
      var match = true;
      for (var offset = 0; offset < needle.Length; offset++)
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
    var count = 0;
    for (var index = 0; index + needle.Length <= haystack.Count; index++)
    {
      var match = true;
      for (var offset = 0; offset < needle.Length; offset++)
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
    await using var session = await OpenAsync();

    await WaitUntilAsync(() => _server.ReceivedBytes.Count >= 8, TimeSpan.FromSeconds(2));

    Assert.That(_server.ReceivedBytes.Take(8), Is.EqualTo(new byte[] { 0x1B, 0x40, 0x1B, 0x74, 0x13, 0x1D, 0x61, 0x0F }));
  }

  [Test]
  public async Task ConnectAsync_Success_HoldsOneConnectionAcrossMultipleJobs()
  {
    _server.ScriptProcessIdEcho(TimeSpan.Zero);
    await using var session = await OpenAsync();

    await session.SendJobAsync(Payload(1), CancellationToken.None);
    await session.SendJobAsync(Payload(2), CancellationToken.None);

    Assert.That(_server.AcceptedConnectionCount, Is.EqualTo(1));
  }

  [Test]
  public async Task SendJobAsync_EchoArrivesBeforeTimeout_ReturnsConfirmedWithFullByteCount()
  {
    _server.ScriptProcessIdEcho(TimeSpan.Zero);
    await using var session = await OpenAsync();
    var payload = Payload();

    var result = await session.SendJobAsync(payload, CancellationToken.None);

    Assert.That(result.Outcome, Is.EqualTo(PrintOutcome.Confirmed));
    Assert.That(result.BytesWritten, Is.EqualTo(payload.Bytes.Length));
  }

  [Test]
  public async Task SendJobAsync_EchoNeverArrives_ReturnsTimeoutAfterJobTimeoutWithBytesWrittenGreaterThanZero()
  {
    _server.ScriptNeverEchoProcessId();
    await using var session = await OpenAsync(TimeSpan.FromMilliseconds(200));

    var result = await session.SendJobAsync(Payload(), CancellationToken.None);

    Assert.That(result.Outcome, Is.EqualTo(PrintOutcome.Timeout));
    Assert.That(result.BytesWritten, Is.GreaterThan(0));
  }

  [Test]
  public async Task SendJobAsync_ConnectionDropsBeforeFirstByte_ReturnsSocketDroppedWithZeroBytes()
  {
    _server.ScriptDropConnectionImmediately();
    await using var session = await OpenAsync();
    await Task.Delay(150);

    var result = await session.SendJobAsync(Payload(), CancellationToken.None);

    Assert.That(result.Outcome, Is.EqualTo(PrintOutcome.SocketDropped));
    Assert.That(result.BytesWritten, Is.Zero);
  }

  [Test]
  public async Task SendJobAsync_ConnectionDropsMidWrite_ReturnsSocketDroppedWithPartialBytesGreaterThanZero()
  {
    _server.ScriptDropConnectionAfterBytes(256);
    await using var session = await OpenAsync();
    var payload = Payload(sizeInBytes: 200000);

    var result = await session.SendJobAsync(payload, CancellationToken.None);

    Assert.That(result.Outcome, Is.EqualTo(PrintOutcome.SocketDropped));
    Assert.That(result.BytesWritten, Is.GreaterThan(0));
    Assert.That(result.BytesWritten, Is.LessThan(payload.Bytes.Length));
  }

  [Test]
  public async Task SendJobAsync_EchoOnReconnectedSocket_IsDiscarded()
  {
    _server.ScriptProcessIdEcho(TimeSpan.Zero);
    await using (var first = await OpenAsync(TimeSpan.FromMilliseconds(300)))
    {
      await first.SendJobAsync(Payload(11), CancellationToken.None);
    }

    _server.ScriptProcessIdEchoCarrying(11);
    await using var second = await OpenAsync(TimeSpan.FromMilliseconds(300));
    var result = await second.SendJobAsync(Payload(12), CancellationToken.None);

    Assert.That(result.Outcome, Is.EqualTo(PrintOutcome.Timeout));
  }

  [Test]
  public async Task QueryStatusAsync_PaperEndMaskSet_ReportsIsPaperEndTrue()
  {
    _server.ScriptDleEotResponse(1, 0x16);
    _server.ScriptDleEotResponse(2, 0x12);
    _server.ScriptDleEotResponse(4, 0x72);
    await using var session = await OpenAsync();

    var status = await session.QueryStatusAsync(CancellationToken.None);

    Assert.That(status.IsPaperEnd, Is.True);
  }

  [Test]
  public async Task QueryStatusAsync_CoverOpenBitSet_ReportsIsCoverOpenTrue()
  {
    _server.ScriptDleEotResponse(1, 0x16);
    _server.ScriptDleEotResponse(2, 0x16);
    _server.ScriptDleEotResponse(4, 0x12);
    await using var session = await OpenAsync();

    var status = await session.QueryStatusAsync(CancellationToken.None);

    Assert.That(status.IsCoverOpen, Is.True);
    Assert.That(status.IsPaperEnd, Is.False);
  }

  [Test]
  public async Task StatusStream_AsbPushedUnprompted_SurfacesWithoutAQuery()
  {
    _server.ScriptAsbOnConnect(0x60, 0x00);
    await using var session = await OpenAsync();

    using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(2));
    PrinterStatusSnapshot? seen = null;
    await foreach (var snapshot in session.StatusStream.WithCancellation(cancellation.Token))
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
    _server.ScriptNeverEchoProcessId();
    await using var session = await OpenAsync(TimeSpan.FromMilliseconds(200));
    var payload = Payload();

    var result = await session.SendJobAsync(payload, CancellationToken.None);

    Assert.That(result.BytesWritten, Is.EqualTo(payload.Bytes.Length));
  }

  [Test]
  public async Task ConnectAsync_HeartbeatEvery10Seconds_KeepsIdleTimeoutAlive()
  {
    await using var session = await OpenAsync(heartbeat: TimeSpan.FromMilliseconds(60));

    await WaitUntilAsync(() => CountSequence(_server.ReceivedBytes, [0x10, 0x04, 0x04]) >= 3, TimeSpan.FromSeconds(3));

    Assert.That(CountSequence(_server.ReceivedBytes, [0x10, 0x04, 0x04]), Is.GreaterThanOrEqualTo(3));
  }

  [TestCase(150)]
  [TestCase(500)]
  public async Task SendJobAsync_JobTimeoutDefaultsTo90Seconds_MatchesPrinterConfigurationDefault(int jobTimeoutMilliseconds)
  {
    _server.ScriptNeverEchoProcessId();
    var jobTimeout = TimeSpan.FromMilliseconds(jobTimeoutMilliseconds);
    await using var session = await OpenAsync(jobTimeout);

    var stopwatch = Stopwatch.StartNew();
    var result = await session.SendJobAsync(Payload(), CancellationToken.None);
    stopwatch.Stop();

    Assert.That(result.Outcome, Is.EqualTo(PrintOutcome.Timeout));
    Assert.That(stopwatch.Elapsed, Is.GreaterThanOrEqualTo(jobTimeout - TimeSpan.FromMilliseconds(50)));
    Assert.That(stopwatch.Elapsed, Is.LessThan(jobTimeout + TimeSpan.FromSeconds(2)));
  }

  [Test]
  public async Task DrainInbound_StatusQueryInFlightWhileEchoAndAsbArrive_DecodesAllThreeWithoutDesynchronising()
  {
    _server.ScriptProcessIdEcho(TimeSpan.Zero);
    _server.ScriptBurstOnProcessIdRequest(0x60, 0x00, 0x16);
    await using var session = await OpenAsync(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(200));

    Task<PrinterStatusSnapshot> pendingQuery = session.QueryStatusAsync(CancellationToken.None);
    await Task.Delay(50);

    var dispatch = await session.SendJobAsync(Payload(21), CancellationToken.None);
    var queried = await pendingQuery;

    List<PrinterStatusSnapshot> pushed = [];
    using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(2));
    try
    {
      await foreach (var snapshot in session.StatusStream.WithCancellation(cancellation.Token))
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

    Assert.That(dispatch.Outcome, Is.EqualTo(PrintOutcome.Confirmed));
    Assert.That(pushed.Any(snapshot => snapshot.IsPaperEnd), Is.True);
    Assert.That(queried.IsOnline, Is.True);
    Assert.That(((EpsonTmT20ivNetworkPrinterSession)session).UnrecognisedInboundByteCount, Is.Zero);
  }

  [Test]
  public async Task StatusStream_AsbErrorBitSet_ReportsIsInErrorStateUsingTheOfflineStatusErrorBit()
  {
    _server.ScriptAsbOnConnect(0x00, 0x40);
    await using var session = await OpenAsync();

    using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(2));
    PrinterStatusSnapshot? seen = null;
    await foreach (var snapshot in session.StatusStream.WithCancellation(cancellation.Token))
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
    _server.ScriptAsbOnConnect(0x00, 0x08);
    await using var session = await OpenAsync();

    using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(2));
    PrinterStatusSnapshot? seen = null;
    await foreach (var snapshot in session.StatusStream.WithCancellation(cancellation.Token))
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
    _server.ScriptProcessIdEcho(TimeSpan.Zero);
    await using var session = await OpenAsync(TimeSpan.FromSeconds(5));
    var payload = Payload(sizeInBytes: 200000);

    var result = await session.SendJobAsync(payload, CancellationToken.None);

    Assert.That(result.Outcome, Is.EqualTo(PrintOutcome.Confirmed));
    Assert.That(result.BytesWritten, Is.EqualTo(payload.Bytes.Length));
  }

  [TestCase(150)]
  [TestCase(600)]
  public async Task QueryStatusAsync_UnansweredQuery_GivesUpAfterTheEndpointsStatusQueryTimeout(int statusQueryTimeoutMilliseconds)
  {
    var statusQueryTimeout = TimeSpan.FromMilliseconds(statusQueryTimeoutMilliseconds);
    await using var session = await OpenAsync(statusQueryTimeout: statusQueryTimeout);

    var stopwatch = Stopwatch.StartNew();
    await session.QueryStatusAsync(CancellationToken.None);
    stopwatch.Stop();

    var expected = statusQueryTimeout + statusQueryTimeout + statusQueryTimeout;
    Assert.That(stopwatch.Elapsed, Is.GreaterThanOrEqualTo(expected - TimeSpan.FromMilliseconds(80)));
    Assert.That(stopwatch.Elapsed, Is.LessThan(expected + TimeSpan.FromSeconds(2)));
  }
}
