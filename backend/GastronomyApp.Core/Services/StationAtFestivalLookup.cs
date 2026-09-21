using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

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

  public async Task<Result<FestivalStation, StationQueueFailure>> FindAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var station = await _stationRepository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
      return Refuse(StationQueueFailureReason.StationUnknown);

    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return Refuse(StationQueueFailureReason.NoRunningFestival);

    var link = await _festivalStationRepository.FindLinkAsync(festival.Id, stationId, cancellationToken);

    if (link is null)
      return Refuse(StationQueueFailureReason.StationNotAtTheFestival);

    return Result<FestivalStation, StationQueueFailure>.Success(link);
  }

  private Result<FestivalStation, StationQueueFailure> Refuse(StationQueueFailureReason reason)
  {
    return Result<FestivalStation, StationQueueFailure>.Failed(new() { Reason = reason });
  }
}
