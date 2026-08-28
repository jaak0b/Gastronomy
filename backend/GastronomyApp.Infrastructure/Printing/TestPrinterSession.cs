using System.Globalization;
using System.Text;
using System.Threading.Channels;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Infrastructure.Printing;

public sealed class TestPrinterSession : IPrinterSession
{
  private readonly IMockFaultRegistry faultRegistry;
  private readonly UTF8Encoding fileEncoding = new(false);
  private readonly TimeSpan jobTimeout;
  private readonly Action onDisposed;
  private readonly Guid printerId;
  private readonly string sessionStartStamp;
  private readonly string slipRootFolder;
  private readonly Channel<PrinterStatusSnapshot> statusChannel = Channel.CreateUnbounded<PrinterStatusSnapshot>();
  private readonly TimeProvider timeProvider;

  public TestPrinterSession(Guid printerId,
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

  public IAsyncEnumerable<PrinterStatusSnapshot> StatusStream => statusChannel.Reader.ReadAllAsync();

  public Task<PrinterStatusSnapshot> QueryStatusAsync(CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var snapshot = CurrentStatus(Probe());

    if (SurfacesInStatus(faultRegistry.GetArmedFault(printerId)))
    {
      faultRegistry.ClearIfOnce(printerId);
    }

    return Task.FromResult(snapshot);
  }

  public async Task<PrintDispatchResult> SendJobAsync(PrintPayload payload, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var fault = faultRegistry.GetArmedFault(printerId);
    var probe = Probe();

    var result = probe.IsWritable
                   ? await DispatchAsync(payload, fault, probe, cancellationToken)
                   : new(PrintOutcome.PrinterError, 0, CurrentStatus(probe), probe.Detail);

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

  private async Task<PrintDispatchResult> DispatchAsync(PrintPayload payload,
                                                        MockFault fault,
                                                        MockFolderProbeResult probe,
                                                        CancellationToken cancellationToken)
  {
    switch (fault)
    {
      case MockFault.None:
        return WriteWholeSlip(payload, probe);
      case MockFault.PaperEnd:
        return new(PrintOutcome.Blocked, 0, CurrentStatus(probe), "Paper end reported by the test printer.");
      case MockFault.CoverOpen:
        return new(PrintOutcome.Blocked, 0, CurrentStatus(probe), "Cover open reported by the test printer.");
      case MockFault.ConnectTimeout:
        return new(PrintOutcome.Unreachable, 0, CurrentStatus(probe), "The test printer could not be reached.");
      case MockFault.DropSocketEarly:
        return new(PrintOutcome.SocketDropped, 0, CurrentStatus(probe), "The test printer connection dropped before the first byte.");
      case MockFault.DropSocketMidJob:
        return WriteHalfSlip(payload, probe);
      case MockFault.UnknownOutcome:
        await Task.Delay(jobTimeout, cancellationToken);
        return new(PrintOutcome.Timeout,
                   payload.Bytes.Length,
                   CurrentStatus(probe),
                   "The test printer never echoed the process id.");
      default:
        return new Never().OfType<PrintDispatchResult>(fault);
    }
  }

  private PrintDispatchResult WriteWholeSlip(PrintPayload payload, MockFolderProbeResult probe)
  {
    var path = SlipFilePath(payload);
    var fileBytes = fileEncoding.GetBytes(payload.RenderedText);
    File.WriteAllBytes(path, fileBytes);
    return new(PrintOutcome.Confirmed, fileBytes.Length, CurrentStatus(probe), path);
  }

  private PrintDispatchResult WriteHalfSlip(PrintPayload payload, MockFolderProbeResult probe)
  {
    var path = SlipFilePath(payload);
    var half = payload.RenderedText[..(payload.RenderedText.Length / 2)];
    var fileBytes = fileEncoding.GetBytes(half);
    File.WriteAllBytes(path, fileBytes);
    return new(PrintOutcome.SocketDropped,
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
    var folderName = StationFolderName(payload);
    var folder = Path.Combine(slipRootFolder, folderName);
    Directory.CreateDirectory(folder);

    var fileName = payload.IsTest
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
    foreach (var character in stationName)
    {
      var keep = char.IsAsciiLetterOrDigit(character) || character == '-' || character == '_';
      builder.Append(keep ? character : '_');
    }

    return builder.ToString();
  }

  private MockFolderProbeResult Probe()
  {
    var probeFile = Path.Combine(slipRootFolder, $"probe-{Guid.NewGuid().ToString("N")}.tmp");
    try
    {
      Directory.CreateDirectory(slipRootFolder);
      File.WriteAllText(probeFile, string.Empty);
      File.Delete(probeFile);
      return new(true, slipRootFolder, slipRootFolder);
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
    {
      return new(false, slipRootFolder, $"{slipRootFolder}: {error.Message}");
    }
  }

  private PrinterStatusSnapshot CurrentStatus(MockFolderProbeResult probe)
  {
    var fault = faultRegistry.GetArmedFault(printerId);

    return new(probe.IsWritable,
               fault == MockFault.PaperEnd,
               false,
               fault == MockFault.CoverOpen,
               !probe.IsWritable,
               probe.IsWritable ? probe.StationFolderPath : probe.Detail,
               timeProvider.GetUtcNow());
  }
}
