using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Services;

public sealed class StationEstimateService
{
  private readonly RunningFestivalLookup _runningFestival;
  private readonly IStationRepository _stationRepository;

  public StationEstimateService(IStationRepository stationRepository, RunningFestivalLookup runningFestival)
  {
    _stationRepository = stationRepository;
    _runningFestival = runningFestival;
  }

  public async Task<IReadOnlyList<Station>> ReadStationsWithOpenWorkAsync(CancellationToken cancellationToken)
  {
    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return [];

    return await _stationRepository.FindAtFestivalWithOpenItemsAsync(festival.Id, cancellationToken);
  }
}
