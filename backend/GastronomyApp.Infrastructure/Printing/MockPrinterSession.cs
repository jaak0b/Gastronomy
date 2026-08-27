using System.Globalization;
using System.Text;
using System.Threading.Channels;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Infrastructure.Printing;

public sealed class MockPrinterSession : IPrinterSession
{
    private readonly PrinterEndpoint endpoint;
    private readonly string slipRootFolder;
    private readonly string sessionStartStamp;
    private readonly IMockFaultRegistry faultRegistry;
    private readonly TimeProvider timeProvider;
    private readonly Action onDisposed;
    private readonly Channel<PrinterStatusSnapshot> statusChannel = Channel.CreateUnbounded<PrinterStatusSnapshot>();
    private readonly UTF8Encoding fileEncoding = new(false);

    public MockPrinterSession(
        PrinterEndpoint endpoint,
        string slipRootFolder,
        string sessionStartStamp,
        IMockFaultRegistry faultRegistry,
        TimeProvider timeProvider,
        Action onDisposed)
    {
        this.endpoint = endpoint;
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

        if (SurfacesInStatus(faultRegistry.GetArmedFault(endpoint.ProductionLocationId)))
        {
            faultRegistry.ClearIfOnce(endpoint.ProductionLocationId);
        }

        return Task.FromResult(snapshot);
    }

    public async Task<PrintDispatchResult> SendJobAsync(PrintPayload payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MockFault fault = faultRegistry.GetArmedFault(endpoint.ProductionLocationId);
        MockFolderProbeResult probe = Probe();

        PrintDispatchResult result = probe.IsWritable
            ? await DispatchAsync(payload, fault, probe, cancellationToken)
            : new PrintDispatchResult(PrintAttemptOutcome.PrinterError, 0, CurrentStatus(probe), probe.Detail);

        faultRegistry.ClearIfOnce(endpoint.ProductionLocationId);
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
                return new PrintDispatchResult(PrintAttemptOutcome.Blocked, 0, CurrentStatus(probe), "Paper end reported by the mock printer.");
            case MockFault.CoverOpen:
                return new PrintDispatchResult(PrintAttemptOutcome.Blocked, 0, CurrentStatus(probe), "Cover open reported by the mock printer.");
            case MockFault.ConnectTimeout:
                return new PrintDispatchResult(PrintAttemptOutcome.Unreachable, 0, CurrentStatus(probe), "The mock printer could not be reached.");
            case MockFault.DropSocketEarly:
                return new PrintDispatchResult(PrintAttemptOutcome.SocketDropped, 0, CurrentStatus(probe), "The mock connection dropped before the first byte.");
            case MockFault.DropSocketMidJob:
                return WriteHalfSlip(payload, probe);
            case MockFault.UnknownOutcome:
                await Task.Delay(endpoint.JobTimeout, cancellationToken);
                return new PrintDispatchResult(
                    PrintAttemptOutcome.Timeout,
                    payload.Bytes.Length,
                    CurrentStatus(probe),
                    "The mock printer never echoed the process id.");
            default:
                return new Never().OfType<PrintDispatchResult>(fault);
        }
    }

    private PrintDispatchResult WriteWholeSlip(PrintPayload payload, MockFolderProbeResult probe)
    {
        string path = SlipFilePath(payload);
        byte[] fileBytes = fileEncoding.GetBytes(payload.RenderedText);
        File.WriteAllBytes(path, fileBytes);
        return new PrintDispatchResult(PrintAttemptOutcome.Confirmed, fileBytes.Length, CurrentStatus(probe), path);
    }

    private PrintDispatchResult WriteHalfSlip(PrintPayload payload, MockFolderProbeResult probe)
    {
        string path = SlipFilePath(payload);
        string half = payload.RenderedText[..(payload.RenderedText.Length / 2)];
        byte[] fileBytes = fileEncoding.GetBytes(half);
        File.WriteAllBytes(path, fileBytes);
        return new PrintDispatchResult(
            PrintAttemptOutcome.SocketDropped,
            fileBytes.Length,
            CurrentStatus(probe),
            $"The mock connection dropped after writing part of {path}.");
    }

    private bool SurfacesInStatus(MockFault fault)
    {
        return fault == MockFault.PaperEnd || fault == MockFault.CoverOpen;
    }

    private string SlipFilePath(PrintPayload payload)
    {
        string folderName = LocationFolderName(payload);
        string folder = Path.Combine(slipRootFolder, folderName);
        Directory.CreateDirectory(folder);

        string fileName = payload.Kind == PrintJobKind.Test
            ? $"{sessionStartStamp}_{folderName}_test-{payload.ProcessId.ToString(CultureInfo.InvariantCulture)}.txt"
            : $"{sessionStartStamp}_{folderName}_slip-{payload.LocationSequenceNumber.ToString("D3", CultureInfo.InvariantCulture)}_print-{(payload.ReprintCount + 1).ToString(CultureInfo.InvariantCulture)}.txt";

        return Path.Combine(folder, fileName);
    }

    private string LocationFolderName(PrintPayload payload)
    {
        return $"{Sanitise(payload.ProductionLocationName)}-{payload.ProductionLocationId.ToString("D")[..8]}";
    }

    private string Sanitise(string locationName)
    {
        StringBuilder builder = new(locationName.Length);
        foreach (char character in locationName)
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
        MockFault fault = faultRegistry.GetArmedFault(endpoint.ProductionLocationId);

        return new PrinterStatusSnapshot(
            probe.IsWritable,
            fault == MockFault.PaperEnd,
            false,
            fault == MockFault.CoverOpen,
            !probe.IsWritable,
            probe.IsWritable ? probe.LocationFolderPath : probe.Detail,
            timeProvider.GetUtcNow());
    }
}
