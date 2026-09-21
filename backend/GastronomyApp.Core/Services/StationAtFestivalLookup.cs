using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Services;

public sealed class StationAtFestivalLookup
{
  private readonly IFestivalStationRepository _festivalStationRepository;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly IStationRepository _stationRepository;

  public StationAtFestivalLookup(IStationRepository stationRepository, IFestivalStationRepository festivalStationRepository, RunningFestivalLookup runningFestival)
  {
    _stationRepository = stationRepository;
    _festivalStationRepository = festivalStationRepository;
    _runningFestival = runningFestival;
  }

  public async Task<ErrorOr<FestivalStation>> FindAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var station = await _stationRepository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
      return Refusal.StationQueue.StationUnknown();

    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return Refusal.StationQueue.NoRunningFestival();

    var link = await _festivalStationRepository.FindLinkAsync(festival.Id, stationId, cancellationToken);

    if (link is null)
      return Refusal.StationQueue.StationNotAtTheFestival();

    return link;
  }
}
