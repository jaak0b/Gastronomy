using System.Collections.Concurrent;
using System.Globalization;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;

namespace GastronomyApp.Infrastructure.Printing;

public sealed record MockFolderProbeResult(bool IsWritable, string StationFolderPath, string Detail);

public sealed class MockPrinterTransport : IPrinterTransport
{
    private const string SlipRootFolderName = "mock-slips";

    private readonly string dataDirectory;
    private readonly IMockFaultRegistry faultRegistry;
    private readonly TimeProvider timeProvider;
    private readonly string sessionStartStamp;
    private readonly ConcurrentDictionary<Guid, bool> openSessions = new();

    public MockPrinterTransport(string dataDirectory, IMockFaultRegistry faultRegistry, TimeProvider timeProvider)
    {
        this.dataDirectory = dataDirectory;
        this.faultRegistry = faultRegistry;
        this.timeProvider = timeProvider;
        sessionStartStamp = timeProvider.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
    }

    public TransportKind Kind
    {
        get { return TransportKind.Mock; }
    }

    public async Task<IPrinterSession> ConnectAsync(PrinterEndpoint endpoint, CancellationToken cancellationToken)
    {
        if (faultRegistry.GetArmedFault(endpoint.StationId) == MockFault.ConnectTimeout)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        if (!openSessions.TryAdd(endpoint.StationId, true))
        {
            throw new InvalidOperationException(
                $"A mock printer session is already open for station {endpoint.StationId}.");
        }

        return new MockPrinterSession(
            endpoint,
            SlipRootFolder(),
            sessionStartStamp,
            faultRegistry,
            timeProvider,
            () => openSessions.TryRemove(endpoint.StationId, out _));
    }

    public string SlipRootFolderPath => SlipRootFolder();

    private string SlipRootFolder()
    {
        return Path.Combine(dataDirectory, SlipRootFolderName);
    }
}
