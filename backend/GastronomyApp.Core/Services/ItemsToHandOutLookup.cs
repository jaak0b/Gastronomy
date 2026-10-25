using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class ItemsToHandOutLookup
{
  private readonly RunningFestivalLookup _runningFestival;
  private readonly IStationRepository _stationRepository;
  private readonly ITablePlanRepository _tablePlanRepository;

  public ItemsToHandOutLookup(IStationRepository stationRepository, ITablePlanRepository tablePlanRepository, RunningFestivalLookup runningFestival)
  {
    _stationRepository = stationRepository;
    _tablePlanRepository = tablePlanRepository;
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

  public async Task<IReadOnlyList<ListedArea>> FindTablePlanAsync(Festival festival, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(festival);

    return await _tablePlanRepository.FindTablePlanAsync(festival.Id, _runningFestival.IsRunning(festival), cancellationToken);
  }

  public async Task<bool> HasItemsToHandOutOrSettleInAreaIfRunningAsync(Festival festival, Guid areaId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(festival);

    return _runningFestival.IsRunning(festival) && await _tablePlanRepository.HasItemsToHandOutOrSettleInAreaAsync(areaId, cancellationToken);
  }

  public async Task<bool> HasItemsToHandOutOrSettleAtTableIfRunningAsync(Festival festival, Guid tableId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(festival);

    return _runningFestival.IsRunning(festival) && await _tablePlanRepository.HasItemsToHandOutOrSettleAtTableAsync(tableId, cancellationToken);
  }
}
