using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Services;

public sealed class StationQueueService
{
  private readonly StationAtFestivalLookup _lookup;
  private readonly IStationOrderRepository _repository;

  public StationQueueService(StationAtFestivalLookup lookup, IStationOrderRepository repository)
  {
    _lookup = lookup;
    _repository = repository;
  }

  public Task<ErrorOr<Station>> ReadQueueAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return _lookup.FindAsync(stationId, cancellationToken).ThenAsync(station => ReadQueueAtAsync(station, cancellationToken));
  }

  public async Task<ErrorOr<Station>> ReadQueueAtAsync(FestivalStation station, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(station);

    var withQueue = await _repository.FindStationWithUnfinishedOrdersAsync(station.FestivalId, station.StationId, cancellationToken);

    if (withQueue is null)
      return Refusal.StationQueue.StationUnknown();

    return withQueue;
  }

  public Task<ErrorOr<IReadOnlyList<StationOrder>>> ReadFulfilledAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return _lookup.FindAsync(stationId, cancellationToken)
                  .ThenAsync(station => _repository.FindFulfilledAtStationAsync(station.FestivalId, station.StationId, cancellationToken));
  }
}
