using GastronomyApp.Api.Options;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Infrastructure.Printing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Printing;

public sealed record PrinterConfigurationEntry(ProductionLocation Location, PrinterConfiguration Configuration);

public interface IPrinterConfigurationSource
{
    public Task<IReadOnlyList<PrinterConfigurationEntry>> LoadEnabledAsync(CancellationToken ct);
}

public interface IPrinterTransportFactory
{
    public IPrinterTransport Create(TransportKind kind);
}

public interface IPrinterFleet
{
    public Task<PrintJobEnsured> EnqueueAsync(Guid locationTicketId, PrintJobKind kind, CancellationToken cancellationToken);

    public Task<IReadOnlyList<Guid>> ReconnectAsync(Guid productionLocationId, CancellationToken cancellationToken);

    public Task TestPrintAsync(Guid productionLocationId, CancellationToken cancellationToken);
}

public sealed class UnknownLocationTicketException : Exception
{
    public UnknownLocationTicketException()
    {
    }

    public UnknownLocationTicketException(string message)
        : base(message)
    {
    }

    public UnknownLocationTicketException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed record RunningWorker(PrinterWorker Worker, CancellationTokenSource Lifetime, Task Loop);

public sealed class PrinterFleet : IPrinterFleet, IHostedService
{
    private readonly IPrinterConfigurationSource configurationSource;
    private readonly IPrinterTransportFactory transportFactory;
    private readonly IPrinterWorkerDataAccess dataAccess;
    private readonly IPrintCallbacks callbacks;
    private readonly EscPosSlipRenderer renderer;
    private readonly PrinterWorkerDomainServices domainServices;
    private readonly TimeProvider timeProvider;
    private readonly ILoggerFactory loggerFactory;
    private readonly Dictionary<string, RunningWorker> workers = [];
    private readonly Lock guard = new();

    private readonly AppLanguage language;

    public PrinterFleet(
        IPrinterConfigurationSource configurationSource,
        IPrinterTransportFactory transportFactory,
        IPrinterWorkerDataAccess dataAccess,
        IPrintCallbacks callbacks,
        EscPosSlipRenderer renderer,
        PrinterWorkerDomainServices domainServices,
        TimeProvider timeProvider,
        AppLanguage language,
        ILoggerFactory loggerFactory)
    {
        this.language = language;
        this.configurationSource = configurationSource;
        this.transportFactory = transportFactory;
        this.dataAccess = dataAccess;
        this.callbacks = callbacks;
        this.renderer = renderer;
        this.domainServices = domainServices;
        this.timeProvider = timeProvider;
        this.loggerFactory = loggerFactory;
    }

    public IReadOnlyList<PrinterWorker> Workers
    {
        get
        {
            lock (guard)
            {
                return [.. workers.Values.Select(running => running.Worker)];
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await ReconcileAsync(cancellationToken);

        foreach (PrinterWorker worker in Workers)
        {
            await worker.RecoverAtStartupAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        List<RunningWorker> running;
        lock (guard)
        {
            running = [.. workers.Values];
            workers.Clear();
        }

        foreach (RunningWorker entry in running)
        {
            await StopWorkerAsync(entry);
        }
    }

    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<PrinterConfigurationEntry> entries = await configurationSource.LoadEnabledAsync(cancellationToken);
        Dictionary<string, List<PrinterConfigurationEntry>> grouped = [];

        foreach (PrinterConfigurationEntry entry in entries)
        {
            string key = KeyFor(entry.Configuration);
            if (!grouped.TryGetValue(key, out List<PrinterConfigurationEntry>? bucket))
            {
                bucket = [];
                grouped[key] = bucket;
            }

            bucket.Add(entry);
        }

        List<RunningWorker> retired = [];
        lock (guard)
        {
            foreach (string key in workers.Keys.Where(existing => !grouped.ContainsKey(existing)).ToList())
            {
                retired.Add(workers[key]);
                workers.Remove(key);
            }
        }

        foreach (RunningWorker entry in retired)
        {
            await StopWorkerAsync(entry);
        }

        foreach (KeyValuePair<string, List<PrinterConfigurationEntry>> group in grouped)
        {
            Guid[] served = [.. group.Value.Select(entry => entry.Location.Id)];
            bool exists;
            lock (guard)
            {
                exists = workers.TryGetValue(group.Key, out RunningWorker? running)
                    && running.Worker.ServedProductionLocationIds.OrderBy(id => id).SequenceEqual(served.OrderBy(id => id));
            }

            if (exists)
            {
                continue;
            }

            RunningWorker? replaced = null;
            lock (guard)
            {
                if (workers.TryGetValue(group.Key, out RunningWorker? previous))
                {
                    replaced = previous;
                    workers.Remove(group.Key);
                }
            }

            if (replaced is not null)
            {
                await StopWorkerAsync(replaced);
            }

            StartWorker(group.Key, group.Value, served);
        }
    }

    public async Task<PrintJobEnsured> EnqueueAsync(
        Guid locationTicketId,
        PrintJobKind kind,
        CancellationToken cancellationToken)
    {
        Guid? productionLocationId = await dataAccess.ResolveProductionLocationAsync(locationTicketId, cancellationToken);
        if (productionLocationId is null)
        {
            throw new UnknownLocationTicketException(
                $"There is no location ticket with id {locationTicketId}, so no print job was created.");
        }

        PrintJobEnsured ensured = await dataAccess.EnsureOpenPrintJobAsync(
            locationTicketId,
            productionLocationId.Value,
            kind,
            cancellationToken);

        if (ensured.WasCreated)
        {
            WorkerFor(productionLocationId.Value).Enqueue(locationTicketId);
        }

        return ensured;
    }

    public async Task<IReadOnlyList<Guid>> ReconnectAsync(Guid productionLocationId, CancellationToken cancellationToken)
    {
        return await WorkerFor(productionLocationId).ReconnectAsync(cancellationToken);
    }

    public async Task TestPrintAsync(Guid productionLocationId, CancellationToken cancellationToken)
    {
        Guid printJobId = await dataAccess.CreatePrintJobAsync(null, productionLocationId, PrintJobKind.Test, cancellationToken);
        WorkerFor(productionLocationId).EnqueueTestPrint(productionLocationId, printJobId);
    }

    private PrinterWorker WorkerFor(Guid productionLocationId)
    {
        lock (guard)
        {
            foreach (RunningWorker running in workers.Values)
            {
                if (running.Worker.ServedProductionLocationIds.Contains(productionLocationId))
                {
                    return running.Worker;
                }
            }
        }

        throw new UnknownLocationTicketException(
            $"No printer worker serves production location {productionLocationId}.");
    }

    private void StartWorker(string key, IReadOnlyList<PrinterConfigurationEntry> group, Guid[] served)
    {
        PrinterConfiguration configuration = group[0].Configuration;
        PrinterEndpoint endpoint = new(
            group[0].Location.Id,
            configuration.TransportKind,
            configuration.Host,
            configuration.Port,
            configuration.AgentIdentifier,
            TimeSpan.FromSeconds(configuration.ConnectTimeoutSeconds),
            TimeSpan.FromSeconds(configuration.JobTimeoutSeconds),
            TimeSpan.FromSeconds(configuration.HeartbeatSeconds),
            TimeSpan.FromSeconds(configuration.ConnectTimeoutSeconds));

        PrinterWorker worker = new(
            endpoint,
            served,
            transportFactory.Create(configuration.TransportKind),
            dataAccess,
            callbacks,
            renderer,
            domainServices,
            timeProvider,
            language,
            loggerFactory.CreateLogger<PrinterWorker>());

        CancellationTokenSource lifetime = new();
        Task loop = worker.RunAsync(lifetime.Token);

        lock (guard)
        {
            workers[key] = new RunningWorker(worker, lifetime, loop);
        }
    }

    private async Task StopWorkerAsync(RunningWorker entry)
    {
        await entry.Lifetime.CancelAsync();
        await Task.WhenAny(entry.Loop, Task.Delay(TimeSpan.FromSeconds(2)));
        entry.Lifetime.Dispose();
    }

    private string KeyFor(PrinterConfiguration configuration)
    {
        return domainServices.EndpointKeyBuilder.Build(
            configuration.TransportKind,
            configuration.Host ?? string.Empty,
            configuration.Port == 0 ? null : configuration.Port,
            configuration.AgentIdentifier ?? string.Empty);
    }
}
