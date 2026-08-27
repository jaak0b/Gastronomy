using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Printing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Printing;

public sealed class DatabasePrinterConfigurationSource : IPrinterConfigurationSource
{
    private readonly IDbContextFactory<GastronomyAppDbContext> contextFactory;

    public DatabasePrinterConfigurationSource(IDbContextFactory<GastronomyAppDbContext> contextFactory)
    {
        this.contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<PrinterConfigurationEntry>> LoadEnabledAsync(CancellationToken ct)
    {
        await using GastronomyAppDbContext context = await contextFactory.CreateDbContextAsync(ct);

        List<Station> stations = await context.Stations
            .Where(station => station.IsActive)
            .OrderBy(station => station.SortOrder)
            .ToListAsync(ct);

        List<PrinterConfiguration> configurations = await context.PrinterConfigurations
            .Where(configuration => configuration.IsEnabled)
            .ToListAsync(ct);

        List<PrinterConfigurationEntry> entries = [];

        foreach (Station station in stations)
        {
            PrinterConfiguration? configuration = configurations
                .FirstOrDefault(candidate => candidate.StationId == station.Id);

            if (configuration is not null)
            {
                entries.Add(new PrinterConfigurationEntry(station, configuration));
            }
        }

        return entries;
    }
}

public sealed class PrinterTransportFactory : IPrinterTransportFactory
{
    private readonly MockPrinterTransport mockTransport;
    private readonly NetworkPrinterTransport networkTransport;

    public PrinterTransportFactory(MockPrinterTransport mockTransport, NetworkPrinterTransport networkTransport)
    {
        this.mockTransport = mockTransport;
        this.networkTransport = networkTransport;
    }

    public IPrinterTransport Create(TransportKind kind)
    {
        return kind switch
        {
            TransportKind.Mock => mockTransport,
            TransportKind.Network => networkTransport,
            TransportKind.Agent => throw new UnsupportedTransportException(
                "The Pi agent transport is not part of this build, so no station may be configured for it."),
            _ => throw new UnsupportedTransportException($"There is no printer transport for {kind}."),
        };
    }
}

public sealed class UnsupportedTransportException : Exception
{
    public UnsupportedTransportException()
    {
    }

    public UnsupportedTransportException(string message)
        : base(message)
    {
    }

    public UnsupportedTransportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
