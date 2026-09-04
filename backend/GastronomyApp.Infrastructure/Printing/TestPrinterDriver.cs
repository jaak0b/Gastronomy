using System.Collections.Concurrent;
using System.Globalization;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Printing;

namespace GastronomyApp.Infrastructure.Printing;

public sealed record MockFolderProbeResult(bool IsWritable, string StationFolderPath, string Detail);

public sealed class TestPrinterDriver : PrinterDriver<TestPrinter>
{
  private const string SlipRootFolderName = "mock-slips";

  private readonly string _dataDirectory;
  private readonly IMockFaultRegistry _faultRegistry;
  private readonly ConcurrentDictionary<Guid, bool> _openSessions = new();
  private readonly string _sessionStartStamp;
  private readonly TimeProvider _timeProvider;

  public TestPrinterDriver(string dataDirectory, IMockFaultRegistry faultRegistry, TimeProvider timeProvider)
  {
    ArgumentNullException.ThrowIfNull(timeProvider);

    _dataDirectory = dataDirectory;
    _faultRegistry = faultRegistry;
    _timeProvider = timeProvider;
    _sessionStartStamp = timeProvider.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
  }

  override public int CharactersPerLine => 48;

  override public string CodePageName => "PC858";

  override public TimeSpan ConnectTimeout => TimeSpan.FromSeconds(3);

  override public TimeSpan JobTimeout => TimeSpan.FromSeconds(90);

  override public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(10);

  override public TimeSpan StatusQueryTimeout => TimeSpan.FromSeconds(3);

  public string SlipRootFolderPath => SlipRootFolder();

  override protected async Task<IPrinterSession> ConnectAsync(TestPrinter printer, CancellationToken cancellationToken)
  {
    if (_faultRegistry.GetArmedFault(printer.Id) == MockFault.ConnectTimeout)
    {
      await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    if (!_openSessions.TryAdd(printer.Id, true))
    {
      throw new InvalidOperationException($"A test printer session is already open for the printer {printer.Name}.");
    }

    return new TestPrinterSession(printer.Id,
                                  JobTimeout,
                                  SlipRootFolder(),
                                  _sessionStartStamp,
                                  _faultRegistry,
                                  _timeProvider,
                                  () => _openSessions.TryRemove(printer.Id, out _));
  }

  private string SlipRootFolder()
  {
    return Path.Combine(_dataDirectory, SlipRootFolderName);
  }
}
