using GastronomyApp.Core.ReadModels;
using GastronomyApp.Infrastructure.QueryRows;
using Mapster;

namespace GastronomyApp.Infrastructure.Projections;

public sealed class StationProjection : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<AdministeredStationRow, AdministeredStation>()
          .Map(administered => administered.StationId, row => row.Station.Id)
          .Map(administered => administered.Name, row => row.Station.Name)
          .Map(administered => administered.SortOrder, row => row.Station.SortOrder)
          .Map(administered => administered.IsActive, row => row.Station.IsActive)
          .Map(administered => administered.HasDevice, row => row.Station.DeviceId != null);
  }
}
