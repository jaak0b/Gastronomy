using System.Diagnostics;
using System.Text;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Infrastructure.Printing;

namespace GastronomyApp.Infrastructure.Tests.Printing;

public class TestPrinterDriverTest
{
  private string dataDirectory = null!;
  private TestPrinterDriver driver = null!;
  private Guid printerId;
  private InMemoryMockFaultRegistry registry = null!;
  private Guid stationId;
  private TestTimeProvider timeProvider = null!;

  [SetUp]
  public void SetUp()
  {
    dataDirectory = Path.Combine(Path.GetTempPath(), "gastronomy-mock-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dataDirectory);
    registry = new();
    timeProvider = new(new(2026, 8, 26, 17, 5, 9, TimeSpan.Zero));
    stationId = Guid.Parse("8f2a1c4b-9d0e-7f6a-3b2c-1d0e9f8a7b6c");
    printerId = Guid.Parse("2c3d4e5f-6a7b-4c8d-9e0f-1a2b3c4d5e6f");
    driver = new(dataDirectory, registry, timeProvider);
  }

  [TearDown]
  public void TearDown()
  {
    if (Directory.Exists(dataDirectory))
    {
      Directory.Delete(dataDirectory, true);
    }
  }

  private TestPrinter Printer(Guid? id = null)
  {
    return new()
           {
             Id = id ?? printerId,
             Name = "Testdrucker"
           };
  }

  private Task<IPrinterSession> OpenAsync(Guid? id = null)
  {
    return driver.ConnectAsync(Printer(id), CancellationToken.None);
  }

  private IPrinterSession OpenWithJobTimeout(TimeSpan jobTimeout)
  {
    return new TestPrinterSession(printerId,
                                  jobTimeout,
                                  Path.Combine(dataDirectory, "mock-slips"),
                                  "20260826-170509",
                                  registry,
                                  timeProvider,
                                  () => { });
  }

  private PrintPayload Payload(bool isTest = false,
                               int stationOrderNumber = 42,
                               int copyNumber = 0,
                               int printerJobId = 7,
                               string renderedText = "KUECHE\r\nBON 042\r\n",
                               Guid? id = null,
                               string name = "Küche")
  {
    return new(printerJobId,
               Encoding.UTF8.GetBytes(renderedText),
               renderedText,
               copyNumber,
               stationOrderNumber,
               id ?? stationId,
               name,
               isTest);
  }

  private string StationFolder(Guid? id = null, string name = "K_che")
  {
    var effective = id ?? stationId;
    return Path.Combine(dataDirectory, "mock-slips", $"{name}-{effective.ToString("D")[..8]}");
  }

  private string[] FilesIn(string folder)
  {
    return Directory.Exists(folder) ? Directory.GetFiles(folder) : [];
  }

  [Test]
  public async Task SendJobAsync_NoFault_WritesOneFilePerSlipWithRenderedTextVerbatim()
  {
    await using var session = await OpenAsync();
    var payload = Payload();

    var result = await session.SendJobAsync(payload, CancellationToken.None);

    Assert.That(result.Outcome, Is.EqualTo(PrintOutcome.Confirmed));
    Assert.That(result.BytesWritten, Is.EqualTo(payload.Bytes.Length));

    var files = FilesIn(StationFolder());
    Assert.That(files, Has.Length.EqualTo(1));
    Assert.That(Path.GetFileName(files[0]), Is.EqualTo("20260826-170509_K_che-8f2a1c4b_slip-042_print-1.txt"));

    var written = await File.ReadAllBytesAsync(files[0]);
    Assert.That(written, Is.EqualTo(new UTF8Encoding(false).GetBytes(payload.RenderedText)));
  }

  [Test]
  public async Task SendJobAsync_PaperEndArmed_ReturnsBlockedWithZeroBytesAndNoFile()
  {
    registry.Arm(printerId, MockFault.PaperEnd, MockFaultMode.Sticky);
    await using var session = await OpenAsync();

    var result = await session.SendJobAsync(Payload(), CancellationToken.None);

    Assert.That(result.Outcome, Is.EqualTo(PrintOutcome.Blocked));
    Assert.That(result.BytesWritten, Is.Zero);
    Assert.That(result.StatusAtEnd.IsPaperEnd, Is.True);
    Assert.That(FilesIn(StationFolder()), Is.Empty);
  }

  [Test]
  public async Task SendJobAsync_CoverOpenArmed_ReturnsBlockedWithZeroBytesAndNoFile()
  {
    registry.Arm(printerId, MockFault.CoverOpen, MockFaultMode.Sticky);
    await using var session = await OpenAsync();

    var result = await session.SendJobAsync(Payload(), CancellationToken.None);

    Assert.That(result.Outcome, Is.EqualTo(PrintOutcome.Blocked));
    Assert.That(result.BytesWritten, Is.Zero);
    Assert.That(result.StatusAtEnd.IsCoverOpen, Is.True);
    Assert.That(FilesIn(StationFolder()), Is.Empty);
  }

  [Test]
  public async Task ConnectAsync_ConnectTimeoutArmed_NeverCompletes()
  {
    registry.Arm(printerId, MockFault.ConnectTimeout, MockFaultMode.Sticky);
    using CancellationTokenSource cancellation = new();

    Task<IPrinterSession> connect = driver.ConnectAsync(Printer(), cancellation.Token);
    var completed = await Task.WhenAny(connect, Task.Delay(250));

    Assert.That(completed, Is.Not.SameAs(connect));
    await cancellation.CancelAsync();
  }

  [Test]
  public async Task SendJobAsync_DropSocketEarlyArmed_ReturnsSocketDroppedZeroBytesNoFileWritten()
  {
    registry.Arm(printerId, MockFault.DropSocketEarly, MockFaultMode.Sticky);
    await using var session = await OpenAsync();

    var result = await session.SendJobAsync(Payload(), CancellationToken.None);

    Assert.That(result.Outcome, Is.EqualTo(PrintOutcome.SocketDropped));
    Assert.That(result.BytesWritten, Is.Zero);
    Assert.That(FilesIn(StationFolder()), Is.Empty);
  }

  [Test]
  public async Task SendJobAsync_DropSocketMidJobArmed_WritesPartialFileAndReturnsSocketDroppedWithPartialBytes()
  {
    registry.Arm(printerId, MockFault.DropSocketMidJob, MockFaultMode.Sticky);
    await using var session = await OpenAsync();
    var payload = Payload(renderedText: new('X', 100));

    var result = await session.SendJobAsync(payload, CancellationToken.None);

    Assert.That(result.Outcome, Is.EqualTo(PrintOutcome.SocketDropped));
    Assert.That(result.BytesWritten, Is.GreaterThan(0));
    Assert.That(result.BytesWritten, Is.LessThan(payload.Bytes.Length));

    var files = FilesIn(StationFolder());
    Assert.That(files, Has.Length.EqualTo(1));
    var content = await File.ReadAllTextAsync(files[0]);
    Assert.That(content, Has.Length.EqualTo(50));
  }

  [Test]
  public async Task SendJobAsync_UnknownOutcomeArmed_AcceptsPayloadWritesNoFileNeverEchoesReturnsTimeout()
  {
    registry.Arm(printerId, MockFault.UnknownOutcome, MockFaultMode.Sticky);
    await using var session = OpenWithJobTimeout(TimeSpan.FromMilliseconds(150));
    var payload = Payload();

    var result = await session.SendJobAsync(payload, CancellationToken.None);

    Assert.That(result.Outcome, Is.EqualTo(PrintOutcome.Timeout));
    Assert.That(result.BytesWritten, Is.EqualTo(payload.Bytes.Length));
    Assert.That(FilesIn(StationFolder()), Is.Empty);
  }

  [Test]
  public async Task Arm_OnceMode_ClearsAfterOneUseAndSubsequentJobSucceeds()
  {
    registry.Arm(printerId, MockFault.PaperEnd, MockFaultMode.Once);
    await using var session = await OpenAsync();

    var first = await session.SendJobAsync(Payload(), CancellationToken.None);
    var second = await session.SendJobAsync(Payload(copyNumber: 1), CancellationToken.None);

    Assert.That(first.Outcome, Is.EqualTo(PrintOutcome.Blocked));
    Assert.That(second.Outcome, Is.EqualTo(PrintOutcome.Confirmed));
  }

  [Test]
  public async Task Arm_StickyMode_StaysArmedAcrossMultipleJobs()
  {
    registry.Arm(printerId, MockFault.PaperEnd, MockFaultMode.Sticky);
    await using var session = await OpenAsync();

    var first = await session.SendJobAsync(Payload(), CancellationToken.None);
    var second = await session.SendJobAsync(Payload(copyNumber: 1), CancellationToken.None);

    Assert.That(first.Outcome, Is.EqualTo(PrintOutcome.Blocked));
    Assert.That(second.Outcome, Is.EqualTo(PrintOutcome.Blocked));
  }

  [Test]
  public async Task SendJobAsync_ReprintKeepsSequenceNumber_WritesPrintNPlusOneFileBesideOriginal()
  {
    await using var session = await OpenAsync();

    await session.SendJobAsync(Payload(renderedText: "original"), CancellationToken.None);
    await session.SendJobAsync(Payload(copyNumber: 1, renderedText: "reprint"), CancellationToken.None);

    var files = FilesIn(StationFolder()).Select(Path.GetFileName).OfType<string>().Order().ToArray();
    Assert.That(files,
                Is.EqualTo(new[]
                           {
                             "20260826-170509_K_che-8f2a1c4b_slip-042_print-1.txt",
                             "20260826-170509_K_che-8f2a1c4b_slip-042_print-2.txt"
                           }));
    Assert.That(await File.ReadAllTextAsync(Path.Combine(StationFolder(), files[0])), Is.EqualTo("original"));
  }

  [Test]
  public async Task SendJobAsync_TwoSessionsSameFolder_DoNotCollide()
  {
    await using (var first = await OpenAsync())
    {
      await first.SendJobAsync(Payload(), CancellationToken.None);
    }

    TestTimeProvider laterClock = new(new(2026, 8, 27, 18, 0, 0, TimeSpan.Zero));
    TestPrinterDriver laterDriver = new(dataDirectory, registry, laterClock);
    await using (var second = await laterDriver.ConnectAsync(Printer(), CancellationToken.None))
    {
      await second.SendJobAsync(Payload(), CancellationToken.None);
    }

    Assert.That(FilesIn(StationFolder()), Has.Length.EqualTo(2));
  }

  [Test]
  public async Task SendJobAsync_TwoStationsSameName_KeptApartByIdSuffix()
  {
    var otherId = Guid.Parse("11112222-3333-4444-5555-666677778888");

    await using (var first = await OpenAsync())
    {
      await first.SendJobAsync(Payload(name: "Theke"), CancellationToken.None);
    }

    await using (var second = await OpenAsync(otherId))
    {
      await second.SendJobAsync(Payload(id: otherId, name: "Theke"), CancellationToken.None);
    }

    Assert.That(FilesIn(StationFolder(name: "Theke")), Has.Length.EqualTo(1));
    Assert.That(FilesIn(StationFolder(otherId, "Theke")), Has.Length.EqualTo(1));
  }

  [Test]
  public async Task ConnectAsync_UnwritableDataDirectory_ReturnsPrinterErrorZeroBytesAndReportsPathAndReason()
  {
    var blockingFile = Path.Combine(dataDirectory, "blocked");
    await File.WriteAllTextAsync(blockingFile, "not a directory");
    TestPrinterDriver blocked = new(blockingFile, registry, timeProvider);

    await using var session = await blocked.ConnectAsync(Printer(), CancellationToken.None);
    var status = await session.QueryStatusAsync(CancellationToken.None);
    var result = await session.SendJobAsync(Payload(), CancellationToken.None);

    Assert.That(status.IsInErrorState, Is.True);
    Assert.That(status.IsOnline, Is.False);
    Assert.That(status.Detail, Does.Contain(blockingFile));
    Assert.That(status.Detail, Has.Length.GreaterThan(blockingFile.Length));
    Assert.That(result.Outcome, Is.EqualTo(PrintOutcome.PrinterError));
    Assert.That(result.BytesWritten, Is.Zero);
  }

  [Test]
  public async Task SendJobAsync_TestSlip_FileNameUsesTestPrefixWithProcessId()
  {
    await using var session = await OpenAsync();

    await session.SendJobAsync(Payload(true, printerJobId: 314), CancellationToken.None);

    var files = FilesIn(StationFolder());
    Assert.That(Path.GetFileName(files[0]), Is.EqualTo("20260826-170509_K_che-8f2a1c4b_test-314.txt"));
  }

  [Test]
  public async Task SendJobAsync_TestSlipWithStationCard_FileContainsBreakGlassUrlAsPlainText()
  {
    await using var session = await OpenAsync();
    var url = "http://192.168.100.123:50000/station/8f2a1c4b9d0e7f6a3b2c1d0e9f8a7b6c";

    await session.SendJobAsync(Payload(true, renderedText: $"TESTBON\r\n{url}\r\n"), CancellationToken.None);

    var content = await File.ReadAllTextAsync(FilesIn(StationFolder())[0]);
    Assert.That(content, Does.Contain(url));
  }

  [Test]
  public async Task SendJobAsync_HasNoArtificialDelay()
  {
    await using var session = await OpenAsync();
    var stopwatch = Stopwatch.StartNew();

    await session.SendJobAsync(Payload(), CancellationToken.None);

    stopwatch.Stop();
    Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(50));
  }

  [Test]
  public async Task StatusStream_UnknownOutcomeArmed_HoldsJobUntilJobTimeoutSecondsExpires()
  {
    registry.Arm(printerId, MockFault.UnknownOutcome, MockFaultMode.Sticky);
    await using var session = OpenWithJobTimeout(TimeSpan.FromMilliseconds(150));

    await session.SendJobAsync(Payload(), CancellationToken.None);

    List<PrinterStatusSnapshot> seen = [];
    using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(2));
    await foreach (var snapshot in session.StatusStream.WithCancellation(cancellation.Token))
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
    registry.Arm(printerId, fault, MockFaultMode.Once);
    Assert.That(registry.GetArmedFault(printerId), Is.EqualTo(fault));
    registry.ClearIfOnce(printerId);
    Assert.That(registry.GetArmedFault(printerId), Is.EqualTo(MockFault.None));

    registry.Arm(printerId, fault, MockFaultMode.Sticky);
    Assert.That(registry.GetArmedFault(printerId), Is.EqualTo(fault));
    registry.ClearIfOnce(printerId);
    Assert.That(registry.GetArmedFault(printerId), Is.EqualTo(fault));

    registry.Arm(printerId, MockFault.None, MockFaultMode.Sticky);
    Assert.That(registry.GetArmedFault(printerId), Is.EqualTo(MockFault.None));
  }

  [Test]
  public async Task QueryStatusAsync_OnceModeFaultConsumedAtPreflight_ClearsSoTheNextJobSucceeds()
  {
    registry.Arm(printerId, MockFault.PaperEnd, MockFaultMode.Once);
    await using var session = await OpenAsync();

    var preflight = await session.QueryStatusAsync(CancellationToken.None);
    var afterPreflight = await session.SendJobAsync(Payload(), CancellationToken.None);

    Assert.That(preflight.IsPaperEnd, Is.True);
    Assert.That(registry.GetArmedFault(printerId), Is.EqualTo(MockFault.None));
    Assert.That(afterPreflight.Outcome, Is.EqualTo(PrintOutcome.Confirmed));
  }

  [Test]
  public async Task SendJobAsync_UnknownOutcomeArmed_HoldsTheJobForTheDriverJobTimeout()
  {
    registry.Arm(printerId, MockFault.UnknownOutcome, MockFaultMode.Sticky);
    await using var session = OpenWithJobTimeout(TimeSpan.FromMilliseconds(150));

    var stopwatch = Stopwatch.StartNew();
    var result = await session.SendJobAsync(Payload(), CancellationToken.None);
    stopwatch.Stop();

    Assert.That(result.Outcome, Is.EqualTo(PrintOutcome.Timeout));
    Assert.That(stopwatch.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(120)));
  }

  [Test]
  public async Task SendJobAsync_DropSocketMidJob_ReportsExactlyTheBytesTheFileReceived()
  {
    registry.Arm(printerId, MockFault.DropSocketMidJob, MockFaultMode.Sticky);
    await using var session = await OpenAsync();
    PrintPayload payload = new(7,
                               new byte[100],
                               "0123456789",
                               0,
                               42,
                               stationId,
                               "Küche",
                               false);

    var result = await session.SendJobAsync(payload, CancellationToken.None);

    var fileLength = new FileInfo(FilesIn(StationFolder())[0]).Length;
    Assert.That(result.BytesWritten, Is.EqualTo((int)fileLength));
    Assert.That(result.BytesWritten, Is.GreaterThan(0));
  }

  [Test]
  public async Task SendJobAsync_FolderBecomesUnwritableAfterConnect_IsDetectedByTheNextJob()
  {
    await using var session = await OpenAsync();
    var first = await session.SendJobAsync(Payload(), CancellationToken.None);

    var slipRoot = Path.Combine(dataDirectory, "mock-slips");
    Directory.Delete(slipRoot, true);
    await File.WriteAllTextAsync(slipRoot, "now a file, not a folder");

    var second = await session.SendJobAsync(Payload(copyNumber: 1), CancellationToken.None);

    Assert.That(first.Outcome, Is.EqualTo(PrintOutcome.Confirmed));
    Assert.That(second.Outcome, Is.EqualTo(PrintOutcome.PrinterError));
    Assert.That(second.BytesWritten, Is.Zero);
    Assert.That(second.StatusAtEnd.IsInErrorState, Is.True);
  }
}
