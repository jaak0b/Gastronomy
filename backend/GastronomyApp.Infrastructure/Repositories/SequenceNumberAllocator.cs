using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class SequenceNumberAllocator : INumberAllocator
{
  private const int SingleRowId = 1;
  private const int FirstNumber = 1;

  private readonly GastronomyAppDbContext _dbContext;

  public SequenceNumberAllocator(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<int> AllocateGlobalOrderNumberAsync(CancellationToken cancellationToken)
  {
    var counters = await LoadCountersAsync(cancellationToken);
    var allocatedValue = counters.NextOrderNumber;
    counters.NextOrderNumber = allocatedValue + 1;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return allocatedValue;
  }

  public async Task<int> AllocateStationOrderNumberAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var station = await _dbContext.Stations
                                  .FirstAsync(candidate => candidate.Id == stationId, cancellationToken);

    var allocatedValue = station.NextStationOrderNumber;
    station.NextStationOrderNumber = allocatedValue + 1;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return allocatedValue;
  }

  public async Task ResetOrderAndStationNumbersAsync(CancellationToken cancellationToken)
  {
    var counters = await LoadCountersAsync(cancellationToken);
    counters.NextOrderNumber = FirstNumber;

    List<Station> stations = await _dbContext.Stations.ToListAsync(cancellationToken);
    foreach (var station in stations)
    {
      station.NextStationOrderNumber = FirstNumber;
    }

    await _dbContext.SaveChangesAsync(cancellationToken);
  }

  private async Task<SequenceCounters> LoadCountersAsync(CancellationToken cancellationToken)
  {
    var counters = await _dbContext.SequenceCounters
                                   .FirstOrDefaultAsync(candidate => candidate.Id == SingleRowId, cancellationToken);

    if (counters is not null)
    {
      return counters;
    }

    SequenceCounters created = new()
                               {
                                 Id = SingleRowId,
                                 NextOrderNumber = FirstNumber
                               };

    _dbContext.SequenceCounters.Add(created);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return created;
  }
}
