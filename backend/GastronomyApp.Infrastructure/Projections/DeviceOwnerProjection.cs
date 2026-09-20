using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using Mapster;

namespace GastronomyApp.Infrastructure.Projections;

public sealed class DeviceOwnerProjection : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<StaffMember, DeviceOwnerRecord>().Map(record => record.Owner, staffMember => new DeviceOwner(DeviceOwnerKind.StaffMember, staffMember.Id));

    config.NewConfig<Station, DeviceOwnerRecord>().Map(record => record.Owner, station => new DeviceOwner(DeviceOwnerKind.Station, station.Id));
  }
}
