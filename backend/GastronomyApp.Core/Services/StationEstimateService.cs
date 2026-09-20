using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Services;

public sealed class StationEstimateService
{
  private readonly ProductionEstimateCalculator _estimateCalculator;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly IStationOrderRepository _stationOrderRepository;
  private readonly IStationRepository _stationRepository;

  public StationEstimateService(IStationRepository stationRepository, IStationOrderRepository stationOrderRepository, RunningFestivalLookup runningFestival, ProductionEstimateCalculator estimateCalculator)
  {
    _stationRepository = stationRepository;
    _stationOrderRepository = stationOrderRepository;
    _runningFestival = runningFestival;
    _estimateCalculator = estimateCalculator;
  }

  public async Task<IReadOnlyList<StationEstimate>> ReadAsync(CancellationToken cancellationToken)
  {
    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return [];

    IReadOnlyCollection<Station> stations = await _stationRepository.FindAtFestivalAsync(festival.Id, cancellationToken);
    IReadOnlyList<StationQueuedWork> queuedWork = await _stationOrderRepository.FindQueuedWorkAtFestivalAsync(festival.Id, cancellationToken);

    return stations.Select(station => new StationEstimate
                                      {
                                        StationId = station.Id,
                                        QueuedMinutes = SumQueuedMinutes(queuedWork, station.Id)
                                      })
                   .ToList();
  }

  private double SumQueuedMinutes(IReadOnlyCollection<StationQueuedWork> queuedWork, Guid stationId)
  {
    return _estimateCalculator.SumQueuedMinutes(queuedWork.Where(row => row.StationId == stationId).Select(row => row.Work));
  }
}
