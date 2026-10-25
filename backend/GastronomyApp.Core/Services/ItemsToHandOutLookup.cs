using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Services;

public sealed class ItemsToHandOutLookup
{
  private readonly RunningFestivalLookup _runningFestival;
  private readonly IStationRepository _stationRepository;

  public ItemsToHandOutLookup(IStationRepository stationRepository, RunningFestivalLookup runningFestival)
  {
    _stationRepository = stationRepository;
    _runningFestival = runningFestival;
  }

  public async Task<IReadOnlyCollection<Guid>> FindStationIdsIfRunningAsync(Festival festival, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(festival);

    if (!_runningFestival.IsRunning(festival))
      return [];

    return await _stationRepository.FindStationIdsWithItemsToHandOutAsync(festival.Id, cancellationToken);
  }

  public async Task<IReadOnlyCollection<Guid>> FindStationIdsAtTheRunningFestivalAsync(CancellationToken cancellationToken)
  {
    var runningFestival = await _runningFestival.FindAsync(cancellationToken);

    if (runningFestival is null)
      return [];

    return await FindStationIdsIfRunningAsync(runningFestival, cancellationToken);
  }
}
