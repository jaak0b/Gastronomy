using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Core.Results;
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

    config.NewConfig<ListedStation, AdminStationView>()
          .Map(view => view.StationId, listed => listed.Station.Id)
          .Map(view => view.Name, listed => listed.Station.Name)
          .Map(view => view.SortOrder, listed => listed.Station.SortOrder)
          .Map(view => view.IsActive, listed => listed.Station.IsActive)
          .Map(view => view.HasDevice, listed => listed.Station.DeviceId != null)
          .Map(view => view.IsAtAnyFestival, listed => _stationService.IsAtAnyFestival(listed.Station));
  }
}
