using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Queries;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class ItemsToHandOutLookup
{
  private readonly IGastronomyDatabase _database;
  private readonly RunningFestivalLookup _runningFestival;

  public ItemsToHandOutLookup(IGastronomyDatabase database, RunningFestivalLookup runningFestival)
  {
    _database = database;
    _runningFestival = runningFestival;
  }

  public async Task<IReadOnlyCollection<Guid>> FindStationIdsIfRunningAsync(Festival festival, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(festival);

    if (!_runningFestival.IsRunning(festival))
      return [];

    return await _database.OrderItems.AsNoTracking().WhereOpen().WhereFestivalIs(festival.Id).Select(item => item.StationOrder.StationId).Distinct().ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyCollection<Guid>> FindStationIdsAtTheRunningFestivalAsync(CancellationToken cancellationToken)
  {
    var runningFestival = await _runningFestival.FindAsync(cancellationToken);

    if (runningFestival is null)
      return [];

    return await FindStationIdsIfRunningAsync(runningFestival, cancellationToken);
  }
}
