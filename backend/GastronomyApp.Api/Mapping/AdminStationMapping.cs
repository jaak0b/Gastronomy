using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminStationMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<Station, AdminStationView>()
          .Map(view => view.StationId, station => station.Id)
          .Map(view => view.HasDevice, station => station.DeviceId != null)
          .Map(view => view.LastSeenAtUtc, station => station.Device == null ? null : (DateTime?)station.Device.LastSeenAtUtc)
          .Map(view => view.HasOutstandingInvitation, station => station.HasOutstandingInvitation())
          .Map(view => view.IsAtTheFestival, station => station.IsAtTheFestival());
  }
}
