using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class SequenceNumberAllocator
{
  private readonly IGastronomyDatabase _database;

  public SequenceNumberAllocator(IGastronomyDatabase database)
  {
    _database = database;
  }

  public async Task<int> AllocateGlobalOrderNumberAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var festival = await _database.Festivals.FirstAsync(candidate => candidate.Id == festivalId, cancellationToken);

    var allocatedValue = festival.NextOrderNumber;
    festival.NextOrderNumber = allocatedValue + 1;
    await _database.SaveChangesAsync(cancellationToken);

    return allocatedValue;
  }

  public async Task<int> AllocateStationOrderNumberAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    var link = await _database.FestivalStations.FirstAsync(candidate => candidate.FestivalId == festivalId && candidate.StationId == stationId, cancellationToken);

    var allocatedValue = link.NextStationOrderNumber;
    link.NextStationOrderNumber = allocatedValue + 1;
    await _database.SaveChangesAsync(cancellationToken);

    return allocatedValue;
  }

  public async Task<int> FindNextStationOrderNumberAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    var highestUsedNumber = await _database.StationOrders.AsNoTracking()
                                           .Where(stationOrder => stationOrder.FestivalId == festivalId && stationOrder.StationId == stationId)
                                           .Select(stationOrder => stationOrder.StationOrderNumber)
                                           .OrderByDescending(number => number)
                                           .FirstOrDefaultAsync(cancellationToken);

    return highestUsedNumber + 1;
  }
}
