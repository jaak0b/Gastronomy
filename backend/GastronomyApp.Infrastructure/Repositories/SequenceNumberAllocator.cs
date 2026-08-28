using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class SequenceNumberAllocator : INumberAllocator
{
  private const int SingleRowId = 1;
  private const int FirstNumber = 1;
  private const int PrinterJobIdMaximumValue = 9999;

  private readonly GastronomyAppDbContext _dbContext;

  public SequenceNumberAllocator(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<int> AllocateGlobalOrderNumberAsync(CancellationToken cancellationToken)
  {
    SequenceCounters counters = await LoadCountersAsync(cancellationToken);
    int allocatedValue = counters.NextOrderNumber;
    counters.NextOrderNumber = allocatedValue + 1;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return allocatedValue;
  }

  public async Task<int> AllocatePrinterJobIdAsync(CancellationToken cancellationToken)
  {
    SequenceCounters counters = await LoadCountersAsync(cancellationToken);
    int allocatedValue = counters.NextPrinterJobId;
    counters.NextPrinterJobId =
        allocatedValue >= PrinterJobIdMaximumValue ? FirstNumber : allocatedValue + 1;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return allocatedValue;
  }

  public async Task<int> AllocateStationOrderNumberAsync(Guid stationId, CancellationToken cancellationToken)
  {
    Station station = await _dbContext.Stations
        .FirstAsync(candidate => candidate.Id == stationId, cancellationToken);

    int allocatedValue = station.NextStationOrderNumber;
    station.NextStationOrderNumber = allocatedValue + 1;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return allocatedValue;
  }

  public async Task ResetOrderAndStationNumbersAsync(CancellationToken cancellationToken)
  {
    SequenceCounters counters = await LoadCountersAsync(cancellationToken);
    counters.NextOrderNumber = FirstNumber;

    List<Station> stations = await _dbContext.Stations.ToListAsync(cancellationToken);
    foreach (Station station in stations)
    {
      station.NextStationOrderNumber = FirstNumber;
    }

    await _dbContext.SaveChangesAsync(cancellationToken);
  }

  private async Task<SequenceCounters> LoadCountersAsync(CancellationToken cancellationToken)
  {
    SequenceCounters? counters = await _dbContext.SequenceCounters
        .FirstOrDefaultAsync(candidate => candidate.Id == SingleRowId, cancellationToken);

    if (counters is not null)
    {
      return counters;
    }

    SequenceCounters created = new()
    {
      Id = SingleRowId,
      NextOrderNumber = FirstNumber,
      NextPrinterJobId = FirstNumber,
    };

    _dbContext.SequenceCounters.Add(created);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return created;
  }
}
