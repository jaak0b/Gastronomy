using System.Globalization;
using System.Text;
using System.Threading.Channels;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Infrastructure.Printing;

public sealed class TestPrinterSession : IPrinterSession
{
  private readonly Guid printerId;
  private readonly TimeSpan jobTimeout;
  private readonly string slipRootFolder;
  private readonly string sessionStartStamp;
  private readonly IMockFaultRegistry faultRegistry;
  private readonly TimeProvider timeProvider;
  private readonly Action onDisposed;
  private readonly Channel<PrinterStatusSnapshot> statusChannel = Channel.CreateUnbounded<PrinterStatusSnapshot>();
  private readonly UTF8Encoding fileEncoding = new(false);

  public TestPrinterSession(
      Guid printerId,
      TimeSpan jobTimeout,
      string slipRootFolder,
      string sessionStartStamp,
      IMockFaultRegistry faultRegistry,
      TimeProvider timeProvider,
      Action onDisposed)
  {
    this.printerId = printerId;
    this.jobTimeout = jobTimeout;
    this.slipRootFolder = slipRootFolder;
    this.sessionStartStamp = sessionStartStamp;
    this.faultRegistry = faultRegistry;
    this.timeProvider = timeProvider;
    this.onDisposed = onDisposed;
    statusChannel.Writer.TryWrite(CurrentStatus(Probe()));
  }

  public IAsyncEnumerable<PrinterStatusSnapshot> StatusStream
  {
    get { return statusChannel.Reader.ReadAllAsync(); }
  }

  public Task<PrinterStatusSnapshot> QueryStatusAsync(CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    PrinterStatusSnapshot snapshot = CurrentStatus(Probe());

    if (SurfacesInStatus(faultRegistry.GetArmedFault(printerId)))
    {
      faultRegistry.ClearIfOnce(printerId);
    }

    return Task.FromResult(snapshot);
  }

  public async Task<PrintDispatchResult> SendJobAsync(PrintPayload payload, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    MockFault fault = faultRegistry.GetArmedFault(printerId);
    MockFolderProbeResult probe = Probe();

    PrintDispatchResult result = probe.IsWritable
        ? await DispatchAsync(payload, fault, probe, cancellationToken)
        : new PrintDispatchResult(PrintOutcome.PrinterError, 0, CurrentStatus(probe), probe.Detail);

    faultRegistry.ClearIfOnce(printerId);
    statusChannel.Writer.TryWrite(result.StatusAtEnd);
    return result;
  }

  public ValueTask DisposeAsync()
  {
    statusChannel.Writer.TryComplete();
    onDisposed();
    return ValueTask.CompletedTask;
  }

  private async Task<PrintDispatchResult> DispatchAsync(
      PrintPayload payload,
      MockFault fault,
      MockFolderProbeResult probe,
      CancellationToken cancellationToken)
  {
    switch (fault)
    {
      case MockFault.None:
        return WriteWholeSlip(payload, probe);
      case MockFault.PaperEnd:
        return new PrintDispatchResult(PrintOutcome.Blocked, 0, CurrentStatus(probe), "Paper end reported by the test printer.");
      case MockFault.CoverOpen:
        return new PrintDispatchResult(PrintOutcome.Blocked, 0, CurrentStatus(probe), "Cover open reported by the test printer.");
      case MockFault.ConnectTimeout:
        return new PrintDispatchResult(PrintOutcome.Unreachable, 0, CurrentStatus(probe), "The test printer could not be reached.");
      case MockFault.DropSocketEarly:
        return new PrintDispatchResult(PrintOutcome.SocketDropped, 0, CurrentStatus(probe), "The test printer connection dropped before the first byte.");
      case MockFault.DropSocketMidJob:
        return WriteHalfSlip(payload, probe);
      case MockFault.UnknownOutcome:
        await Task.Delay(jobTimeout, cancellationToken);
        return new PrintDispatchResult(
            PrintOutcome.Timeout,
            payload.Bytes.Length,
            CurrentStatus(probe),
            "The test printer never echoed the process id.");
      default:
        return new Never().OfType<PrintDispatchResult>(fault);
    }
  }

  private PrintDispatchResult WriteWholeSlip(PrintPayload payload, MockFolderProbeResult probe)
  {
    string path = SlipFilePath(payload);
    byte[] fileBytes = fileEncoding.GetBytes(payload.RenderedText);
    File.WriteAllBytes(path, fileBytes);
    return new PrintDispatchResult(PrintOutcome.Confirmed, fileBytes.Length, CurrentStatus(probe), path);
  }

  private PrintDispatchResult WriteHalfSlip(PrintPayload payload, MockFolderProbeResult probe)
  {
    string path = SlipFilePath(payload);
    string half = payload.RenderedText[..(payload.RenderedText.Length / 2)];
    byte[] fileBytes = fileEncoding.GetBytes(half);
    File.WriteAllBytes(path, fileBytes);
    return new PrintDispatchResult(
        PrintOutcome.SocketDropped,
        fileBytes.Length,
        CurrentStatus(probe),
        $"The test printer connection dropped after writing part of {path}.");
  }

  private bool SurfacesInStatus(MockFault fault)
  {
    return fault == MockFault.PaperEnd || fault == MockFault.CoverOpen;
  }

  private string SlipFilePath(PrintPayload payload)
  {
    string folderName = StationFolderName(payload);
    string folder = Path.Combine(slipRootFolder, folderName);
    Directory.CreateDirectory(folder);

    string fileName = payload.IsTest
        ? $"{sessionStartStamp}_{folderName}_test-{payload.PrinterJobId.ToString(CultureInfo.InvariantCulture)}.txt"
        : $"{sessionStartStamp}_{folderName}_slip-{payload.StationOrderNumber.ToString("D3", CultureInfo.InvariantCulture)}_print-{(payload.CopyNumber + 1).ToString(CultureInfo.InvariantCulture)}.txt";

    return Path.Combine(folder, fileName);
  }

  private string StationFolderName(PrintPayload payload)
  {
    return $"{Sanitise(payload.StationName)}-{payload.StationId.ToString("D")[..8]}";
  }

  private string Sanitise(string stationName)
  {
    StringBuilder builder = new(stationName.Length);
    foreach (char character in stationName)
    {
      bool keep = char.IsAsciiLetterOrDigit(character) || character == '-' || character == '_';
      builder.Append(keep ? character : '_');
    }

    return builder.ToString();
  }

  private MockFolderProbeResult Probe()
  {
    string probeFile = Path.Combine(slipRootFolder, $"probe-{Guid.NewGuid().ToString("N")}.tmp");
    try
    {
      Directory.CreateDirectory(slipRootFolder);
      File.WriteAllText(probeFile, string.Empty);
      File.Delete(probeFile);
      return new MockFolderProbeResult(true, slipRootFolder, slipRootFolder);
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
    {
      return new MockFolderProbeResult(false, slipRootFolder, $"{slipRootFolder}: {error.Message}");
    }
  }

  private PrinterStatusSnapshot CurrentStatus(MockFolderProbeResult probe)
  {
    MockFault fault = faultRegistry.GetArmedFault(printerId);

    return new PrinterStatusSnapshot(
        probe.IsWritable,
        fault == MockFault.PaperEnd,
        false,
        fault == MockFault.CoverOpen,
        !probe.IsWritable,
        probe.IsWritable ? probe.StationFolderPath : probe.Detail,
        timeProvider.GetUtcNow());
  }
}
