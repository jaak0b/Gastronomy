using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class StationEstimateMapping
{
  private readonly StationService _stationService;

  public StationEstimateMapping(StationService stationService)
  {
    _stationService = stationService;
  }

  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<Station, StationEstimateView>().Map(view => view.StationId, station => station.Id).Map(view => view.QueuedMinutes, station => _stationService.QueuedMinutesOf(station));
  }
}
