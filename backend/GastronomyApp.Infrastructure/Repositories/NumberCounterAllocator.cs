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

    public Task<int> AllocateGlobalOrderNumberAsync(CancellationToken cancellationToken)
    {
        return AllocateAsync(
            NumberCounterKind.GlobalOrder,
            Guid.Empty,
            string.Empty,
            UnboundedMaximumValue,
            cancellationToken);
    }

    public Task<int> AllocateStationSequenceNumberAsync(
        Guid stationId,
        CancellationToken cancellationToken)
    {
        return AllocateAsync(
            NumberCounterKind.StationSequence,
            stationId,
            string.Empty,
            UnboundedMaximumValue,
            cancellationToken);
    }

    public Task<int> AllocatePrinterProcessIdAsync(string printerEndpointKey, CancellationToken cancellationToken)
    {
        return AllocateAsync(
            NumberCounterKind.PrinterProcessId,
            Guid.Empty,
            printerEndpointKey,
            PrinterProcessIdMaximumValue,
            cancellationToken);
    }

    public async Task ResetOrderAndSlipNumbersAsync(CancellationToken cancellationToken)
    {
        List<NumberCounter> counters = await _dbContext.NumberCounters
            .Where(candidate => candidate.CounterKind == NumberCounterKind.GlobalOrder
                || candidate.CounterKind == NumberCounterKind.StationSequence)
            .ToListAsync(cancellationToken);

        _dbContext.NumberCounters.RemoveRange(counters);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> AllocateAsync(
        NumberCounterKind counterKind,
        Guid stationId,
        string printerEndpointKey,
        int maximumValue,
        CancellationToken cancellationToken)
    {
        NumberCounter? counter = await _dbContext.NumberCounters.FirstOrDefaultAsync(
            candidate => candidate.CounterKind == counterKind
                && candidate.StationId == stationId
                && candidate.PrinterEndpointKey == printerEndpointKey,
            cancellationToken);

        if (counter is null)
        {
            _dbContext.NumberCounters.Add(new NumberCounter
            {
                CounterKind = counterKind,
                StationId = stationId,
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
