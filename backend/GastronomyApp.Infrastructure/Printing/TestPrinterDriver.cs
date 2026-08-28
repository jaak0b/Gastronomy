using System.Collections.Concurrent;
using System.Globalization;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Printing;

namespace GastronomyApp.Infrastructure.Printing;

public sealed record MockFolderProbeResult(bool IsWritable, string StationFolderPath, string Detail);

public sealed class TestPrinterDriver : PrinterDriver<TestPrinter>
{
    private const string SlipRootFolderName = "mock-slips";

    private readonly string dataDirectory;
    private readonly IMockFaultRegistry faultRegistry;
    private readonly TimeProvider timeProvider;
    private readonly string sessionStartStamp;
    private readonly ConcurrentDictionary<Guid, bool> openSessions = new();

    public TestPrinterDriver(string dataDirectory, IMockFaultRegistry faultRegistry, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.dataDirectory = dataDirectory;
        this.faultRegistry = faultRegistry;
        this.timeProvider = timeProvider;
        sessionStartStamp = timeProvider.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
    }

    public override int CharactersPerLine => 48;

    public override string CodePageName => "PC858";

    public override TimeSpan ConnectTimeout => TimeSpan.FromSeconds(3);

    public override TimeSpan JobTimeout => TimeSpan.FromSeconds(90);

    public override TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(10);

    public override TimeSpan StatusQueryTimeout => TimeSpan.FromSeconds(3);

    public string SlipRootFolderPath => SlipRootFolder();

    protected override async Task<IPrinterSession> ConnectAsync(TestPrinter printer, CancellationToken cancellationToken)
    {
        if (faultRegistry.GetArmedFault(printer.Id) == MockFault.ConnectTimeout)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        if (!openSessions.TryAdd(printer.Id, true))
        {
            throw new InvalidOperationException(
                $"A test printer session is already open for the printer {printer.Name}.");
        }

        return new TestPrinterSession(
            printer.Id,
            JobTimeout,
            SlipRootFolder(),
            sessionStartStamp,
            faultRegistry,
            timeProvider,
            () => openSessions.TryRemove(printer.Id, out _));
    }

    private string SlipRootFolder()
    {
        return Path.Combine(dataDirectory, SlipRootFolderName);
    }
}
