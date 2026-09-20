using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class StationStanding
{
  private readonly IClock _clock;
  private readonly IFestivalRepository _festivalRepository;
  private readonly IFestivalStationRepository _festivalStationRepository;
  private readonly IStationRepository _stationRepository;

  public StationStanding(IStationRepository stationRepository,
                         IFestivalRepository festivalRepository,
                         IFestivalStationRepository festivalStationRepository,
                         IClock clock)
  {
    _stationRepository = stationRepository;
    _festivalRepository = festivalRepository;
    _festivalStationRepository = festivalStationRepository;
    _clock = clock;
  }

  public async Task<Result<StationAtFestival, StationQueueFailure>> FindAsync(Guid stationId,
                                                                             CancellationToken cancellationToken)
  {
    Station? station = await _stationRepository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
    {
      return Refuse(StationQueueFailureReason.StationUnknown);
    }

    Festival? festival = await _festivalRepository.FindRunningAsync(_clock.UtcNow, cancellationToken);

    if (festival is null)
    {
      return Refuse(StationQueueFailureReason.NoRunningFestival);
    }

    FestivalStation? link = await _festivalStationRepository.FindLinkAsync(festival.Id, stationId, cancellationToken);

    if (link is null)
    {
      return Refuse(StationQueueFailureReason.StationNotAtTheFestival);
    }

    return Result<StationAtFestival, StationQueueFailure>.Success(new()
                                                                 {
                                                                   Station = station,
                                                                   FestivalId = festival.Id
                                                                 });
  }

  private Result<StationAtFestival, StationQueueFailure> Refuse(StationQueueFailureReason reason)
  {
    return Result<StationAtFestival, StationQueueFailure>.Failed(new()
                                                                {
                                                                  Reason = reason
                                                                });
  }
}
