using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class NumberCounterAllocator : INumberAllocator
{
    private const int UnboundedMaximumValue = int.MaxValue;
    private const int PrinterProcessIdMaximumValue = 9999;

    private readonly GastronomyAppDbContext _dbContext;

    public NumberCounterAllocator(GastronomyAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> AllocateGlobalOrderNumberAsync(Guid eventSessionId, CancellationToken cancellationToken)
    {
        return AllocateAsync(
            NumberCounterKind.GlobalOrder,
            eventSessionId,
            Guid.Empty,
            string.Empty,
            UnboundedMaximumValue,
            cancellationToken);
    }

    public Task<int> AllocateLocationSequenceNumberAsync(
        Guid eventSessionId,
        Guid productionLocationId,
        CancellationToken cancellationToken)
    {
        return AllocateAsync(
            NumberCounterKind.LocationSequence,
            eventSessionId,
            productionLocationId,
            string.Empty,
            UnboundedMaximumValue,
            cancellationToken);
    }

    public Task<int> AllocatePrinterProcessIdAsync(string printerEndpointKey, CancellationToken cancellationToken)
    {
        return AllocateAsync(
            NumberCounterKind.PrinterProcessId,
            Guid.Empty,
            Guid.Empty,
            printerEndpointKey,
            PrinterProcessIdMaximumValue,
            cancellationToken);
    }

    private async Task<int> AllocateAsync(
        NumberCounterKind counterKind,
        Guid eventSessionId,
        Guid productionLocationId,
        string printerEndpointKey,
        int maximumValue,
        CancellationToken cancellationToken)
    {
        NumberCounter? counter = await _dbContext.NumberCounters.FirstOrDefaultAsync(
            candidate => candidate.CounterKind == counterKind
                && candidate.EventSessionId == eventSessionId
                && candidate.ProductionLocationId == productionLocationId
                && candidate.PrinterEndpointKey == printerEndpointKey,
            cancellationToken);

        if (counter is null)
        {
            _dbContext.NumberCounters.Add(new NumberCounter
            {
                CounterKind = counterKind,
                EventSessionId = eventSessionId,
                ProductionLocationId = productionLocationId,
                PrinterEndpointKey = printerEndpointKey,
                NextValue = 2,
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            return 1;
        }

        int allocatedValue = counter.NextValue;
        counter.NextValue = allocatedValue >= maximumValue ? 1 : allocatedValue + 1;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return allocatedValue;
    }
}
