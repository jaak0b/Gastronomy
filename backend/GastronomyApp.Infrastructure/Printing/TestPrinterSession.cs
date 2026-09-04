using System.Globalization;
using System.Text;
using System.Threading.Channels;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Infrastructure.Printing;

public sealed class TestPrinterSession : IPrinterSession
{
  private readonly IMockFaultRegistry _faultRegistry;
  private readonly UTF8Encoding _fileEncoding = new(false);
  private readonly TimeSpan _jobTimeout;
  private readonly Action _onDisposed;
  private readonly Guid _printerId;
  private readonly string _sessionStartStamp;
  private readonly string _slipRootFolder;
  private readonly Channel<PrinterStatusSnapshot> statusChannel = Channel.CreateUnbounded<PrinterStatusSnapshot>();
  private readonly TimeProvider _timeProvider;

  public TestPrinterSession(Guid printerId,
                            TimeSpan jobTimeout,
                            string slipRootFolder,
                            string sessionStartStamp,
                            IMockFaultRegistry faultRegistry,
                            TimeProvider timeProvider,
                            Action onDisposed)
  {
    _printerId = printerId;
    _jobTimeout = jobTimeout;
    _slipRootFolder = slipRootFolder;
    _sessionStartStamp = sessionStartStamp;
    _faultRegistry = faultRegistry;
    _timeProvider = timeProvider;
    _onDisposed = onDisposed;
    statusChannel.Writer.TryWrite(CurrentStatus(Probe()));
  }

  public IAsyncEnumerable<PrinterStatusSnapshot> StatusStream => statusChannel.Reader.ReadAllAsync();

  public Task<PrinterStatusSnapshot> QueryStatusAsync(CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var snapshot = CurrentStatus(Probe());

    if (SurfacesInStatus(_faultRegistry.GetArmedFault(_printerId)))
    {
      _faultRegistry.ClearIfOnce(_printerId);
    }

    return Task.FromResult(snapshot);
  }

  public async Task<PrintDispatchResult> SendJobAsync(PrintPayload payload, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var fault = _faultRegistry.GetArmedFault(_printerId);
    var probe = Probe();

    var result = probe.IsWritable
                   ? await DispatchAsync(payload, fault, probe, cancellationToken)
                   : new(PrintOutcome.PrinterError, 0, CurrentStatus(probe), probe.Detail);

    _faultRegistry.ClearIfOnce(_printerId);
    statusChannel.Writer.TryWrite(result.StatusAtEnd);
    return result;
  }

  public ValueTask DisposeAsync()
  {
    statusChannel.Writer.TryComplete();
    _onDisposed();
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
        await Task.Delay(_jobTimeout, cancellationToken);
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
    var fileBytes = _fileEncoding.GetBytes(payload.RenderedText);
    File.WriteAllBytes(path, fileBytes);
    return new(PrintOutcome.Confirmed, fileBytes.Length, CurrentStatus(probe), path);
  }

  private PrintDispatchResult WriteHalfSlip(PrintPayload payload, MockFolderProbeResult probe)
  {
    var path = SlipFilePath(payload);
    var half = payload.RenderedText[..(payload.RenderedText.Length / 2)];
    var fileBytes = _fileEncoding.GetBytes(half);
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
    var folder = Path.Combine(_slipRootFolder, folderName);
    Directory.CreateDirectory(folder);

    var fileName = payload.IsTest
                     ? $"{_sessionStartStamp}_{folderName}_test-{payload.PrinterJobId.ToString(CultureInfo.InvariantCulture)}.txt"
                     : $"{_sessionStartStamp}_{folderName}_slip-{payload.StationOrderNumber.ToString("D3", CultureInfo.InvariantCulture)}_print-{(payload.CopyNumber + 1).ToString(CultureInfo.InvariantCulture)}.txt";

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
    var probeFile = Path.Combine(_slipRootFolder, $"probe-{Guid.NewGuid().ToString("N")}.tmp");
    try
    {
      Directory.CreateDirectory(_slipRootFolder);
      File.WriteAllText(probeFile, string.Empty);
      File.Delete(probeFile);
      return new(true, _slipRootFolder, _slipRootFolder);
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
    {
      return new(false, _slipRootFolder, $"{_slipRootFolder}: {error.Message}");
    }
  }

  private PrinterStatusSnapshot CurrentStatus(MockFolderProbeResult probe)
  {
    var fault = _faultRegistry.GetArmedFault(_printerId);

    return new(probe.IsWritable,
               fault == MockFault.PaperEnd,
               false,
               fault == MockFault.CoverOpen,
               !probe.IsWritable,
               probe.IsWritable ? probe.StationFolderPath : probe.Detail,
               _timeProvider.GetUtcNow());
  }
}
