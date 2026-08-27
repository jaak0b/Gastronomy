using System.Collections.Concurrent;
using System.Globalization;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;

namespace GastronomyApp.Infrastructure.Printing;

public sealed record MockFolderProbeResult(bool IsWritable, string LocationFolderPath, string Detail);

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
        if (faultRegistry.GetArmedFault(endpoint.ProductionLocationId) == MockFault.ConnectTimeout)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        if (!openSessions.TryAdd(endpoint.ProductionLocationId, true))
        {
            throw new InvalidOperationException(
                $"A mock printer session is already open for production location {endpoint.ProductionLocationId}.");
        }

        return new MockPrinterSession(
            endpoint,
            SlipRootFolder(),
            sessionStartStamp,
            faultRegistry,
            timeProvider,
            () => openSessions.TryRemove(endpoint.ProductionLocationId, out _));
    }

    private string SlipRootFolder()
    {
        return Path.Combine(dataDirectory, SlipRootFolderName);
    }
}
