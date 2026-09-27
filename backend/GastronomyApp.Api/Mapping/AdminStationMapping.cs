using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminStationMapping : IMappingRegistration
{
  private readonly StationService _stationService;

  public AdminStationMapping(StationService stationService)
  {
    _stationService = stationService;
  }

  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<Station, AdminStationView>()
          .Map(view => view.StationId, station => station.Id)
          .Map(view => view.HasDevice, station => station.DeviceId != null)
          .Map(view => view.IsAtAnyFestival, station => _stationService.IsAtAnyFestival(station));
  }
}
