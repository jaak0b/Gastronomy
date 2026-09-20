using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Services;

public sealed class StationEstimateService
{
  private readonly IClock _clock;
  private readonly ProductionEstimateCalculator _estimateCalculator;
  private readonly IFestivalRepository _festivalRepository;
  private readonly IStationOrderRepository _stationOrderRepository;
  private readonly IStationRepository _stationRepository;

  public StationEstimateService(IStationRepository stationRepository,
                                IStationOrderRepository stationOrderRepository,
                                IFestivalRepository festivalRepository,
                                ProductionEstimateCalculator estimateCalculator,
                                IClock clock)
  {
    _stationRepository = stationRepository;
    _stationOrderRepository = stationOrderRepository;
    _festivalRepository = festivalRepository;
    _estimateCalculator = estimateCalculator;
    _clock = clock;
  }

  public async Task<IReadOnlyList<StationEstimate>> ReadAsync(CancellationToken cancellationToken)
  {
    Festival? festival = await _festivalRepository.FindRunningAsync(_clock.UtcNow, cancellationToken);

    if (festival is null)
    {
      return [];
    }

    IReadOnlyCollection<Station> stations =
      await _stationRepository.FindAtFestivalAsync(festival.Id, cancellationToken);
    IReadOnlyList<StationQueuedWork> queuedWork =
      await _stationOrderRepository.FindQueuedWorkAtFestivalAsync(festival.Id, cancellationToken);

    return
    [
      .. stations.Select(station => new StationEstimate
                                    {
                                      StationId = station.Id,
                                      QueuedMinutes = SumQueuedMinutes(queuedWork, station.Id)
                                    })
    ];
  }

  private double SumQueuedMinutes(IReadOnlyCollection<StationQueuedWork> queuedWork, Guid stationId)
  {
    return _estimateCalculator.SumQueuedMinutes(queuedWork
                                               .Where(row => row.StationId == stationId)
                                               .Select(row => row.Work));
  }
}
