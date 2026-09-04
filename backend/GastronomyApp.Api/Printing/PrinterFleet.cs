using GastronomyApp.Api.Options;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Printing;
using GastronomyApp.Infrastructure.Printing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Printing;

public sealed record PrinterWithStations(Printer Printer, IReadOnlyList<Guid> StationIds);

public interface IPrinterSource
{
  public Task<IReadOnlyList<PrinterWithStations>> LoadActiveAsync(CancellationToken ct);
}

public interface IPrinterFleet
{
  public Task<PrintJobEnsured> EnqueueAsync(Guid stationOrderId, CancellationToken cancellationToken);

  public Task<IReadOnlyList<Guid>> ReconnectAsync(Guid printerId, CancellationToken cancellationToken);

  public Task TestPrintAsync(Guid printerId, CancellationToken cancellationToken);
}

public sealed class UnknownStationOrderException : Exception
{
  public UnknownStationOrderException()
  {
  }

  public UnknownStationOrderException(string message)
    : base(message)
  {
  }

  public UnknownStationOrderException(string message, Exception innerException)
    : base(message, innerException)
  {
  }
}

public sealed record RunningWorker(PrinterWorker Worker, CancellationTokenSource Lifetime, Task Loop);

public sealed class PrinterFleet : IPrinterFleet, IHostedService
{
  private readonly IPrintCallbacks _callbacks;
  private readonly IPrinterWorkerDataAccess _dataAccess;
  private readonly PrinterWorkerDomainServices _domainServices;
  private readonly PrinterDriverRegistry _driverRegistry;
  private readonly Lock _guard = new();

  private readonly AppLanguage _language;
  private readonly ILoggerFactory _loggerFactory;
  private readonly IPrinterSource _printerSource;
  private readonly EscPosSlipRenderer _renderer;
  private readonly TimeProvider _timeProvider;
  private readonly Dictionary<Guid, RunningWorker> _workers = [];

  public PrinterFleet(IPrinterSource printerSource,
                      PrinterDriverRegistry driverRegistry,
                      IPrinterWorkerDataAccess dataAccess,
                      IPrintCallbacks callbacks,
                      EscPosSlipRenderer renderer,
                      PrinterWorkerDomainServices domainServices,
                      TimeProvider timeProvider,
                      AppLanguage language,
                      ILoggerFactory loggerFactory)
  {
    _language = language;
    _printerSource = printerSource;
    _driverRegistry = driverRegistry;
    _dataAccess = dataAccess;
    _callbacks = callbacks;
    _renderer = renderer;
    _domainServices = domainServices;
    _timeProvider = timeProvider;
    _loggerFactory = loggerFactory;
  }

  public IReadOnlyList<PrinterWorker> Workers
  {
    get
    {
      lock (_guard)
      {
        return [.. _workers.Values.Select(running => running.Worker)];
      }
    }
  }

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    await ReconcileAsync(cancellationToken);

    foreach (var worker in Workers)
    {
      await worker.RecoverAtStartupAsync(cancellationToken);
    }
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    List<RunningWorker> running;
    lock (_guard)
    {
      running = [.. _workers.Values];
      _workers.Clear();
    }

    foreach (var entry in running)
    {
      await StopWorkerAsync(entry);
    }
  }

  public async Task<PrintJobEnsured> EnqueueAsync(Guid stationOrderId, CancellationToken cancellationToken)
  {
    var ensured = await _dataAccess.EnsureNextCopyAsync(stationOrderId, cancellationToken);

    if (ensured.StationId is null)
    {
      throw new UnknownStationOrderException($"There is no station order with id {stationOrderId}, so no print job was created.");
    }

    if (ensured.PrintJobId is not null)
    {
      WorkerForStation(ensured.StationId.Value).Enqueue(ensured.PrintJobId.Value);
    }

    return ensured;
  }

  public async Task<IReadOnlyList<Guid>> ReconnectAsync(Guid printerId, CancellationToken cancellationToken)
  {
    return await WorkerForPrinter(printerId).ReconnectAsync(cancellationToken);
  }

  public async Task TestPrintAsync(Guid printerId, CancellationToken cancellationToken)
  {
    WorkerForPrinter(printerId).EnqueueTestPrint(Guid.NewGuid());
    await Task.CompletedTask;
  }

  public async Task ReconcileAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<PrinterWithStations> entries = await _printerSource.LoadActiveAsync(cancellationToken);
    Dictionary<Guid, PrinterWithStations> wanted = entries.ToDictionary(entry => entry.Printer.Id);

    List<RunningWorker> retired = [];
    lock (_guard)
    {
      foreach (var printerId in _workers.Keys.Where(existing => !wanted.ContainsKey(existing)).ToList())
      {
        retired.Add(_workers[printerId]);
        _workers.Remove(printerId);
      }
    }

    foreach (var entry in retired)
    {
      await StopWorkerAsync(entry);
    }

    foreach (KeyValuePair<Guid, PrinterWithStations> wantedPrinter in wanted)
    {
      Guid[] served = [.. wantedPrinter.Value.StationIds];
      bool alreadyRunning;
      lock (_guard)
      {
        alreadyRunning = _workers.TryGetValue(wantedPrinter.Key, out var running)
                         && running.Worker.ServedStationIds.OrderBy(id => id).SequenceEqual(served.OrderBy(id => id));
      }

      if (alreadyRunning)
      {
        continue;
      }

      RunningWorker? replaced = null;
      lock (_guard)
      {
        if (_workers.TryGetValue(wantedPrinter.Key, out var previous))
        {
          replaced = previous;
          _workers.Remove(wantedPrinter.Key);
        }
      }

      if (replaced is not null)
      {
        await StopWorkerAsync(replaced);
      }

      StartWorker(wantedPrinter.Value, served);
    }
  }

  private PrinterWorker WorkerForStation(Guid stationId)
  {
    lock (_guard)
    {
      foreach (var running in _workers.Values)
      {
        if (running.Worker.ServedStationIds.Contains(stationId))
        {
          return running.Worker;
        }
      }
    }

    throw new UnknownStationOrderException($"No printer worker serves station {stationId}.");
  }

  private PrinterWorker WorkerForPrinter(Guid printerId)
  {
    lock (_guard)
    {
      if (_workers.TryGetValue(printerId, out var running))
      {
        return running.Worker;
      }
    }

    throw new UnknownStationOrderException($"No printer worker is running for printer {printerId}.");
  }

  private void StartWorker(PrinterWithStations entry, Guid[] served)
  {
    PrinterWorker worker = new(entry.Printer,
                               served,
                               _driverRegistry.For(entry.Printer),
                               _dataAccess,
                               _callbacks,
                               _renderer,
                               _domainServices,
                               _timeProvider,
                               _language,
                               _loggerFactory.CreateLogger<PrinterWorker>());

    CancellationTokenSource lifetime = new();
    var loop = worker.RunAsync(lifetime.Token);

    lock (_guard)
    {
      _workers[entry.Printer.Id] = new(worker, lifetime, loop);
    }
  }

  private async Task StopWorkerAsync(RunningWorker entry)
  {
    await entry.Lifetime.CancelAsync();
    await Task.WhenAny(entry.Loop, Task.Delay(TimeSpan.FromSeconds(2)));
    entry.Lifetime.Dispose();
  }
}
