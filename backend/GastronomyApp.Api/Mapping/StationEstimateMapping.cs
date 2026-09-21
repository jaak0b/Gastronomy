using GastronomyApp.Contracts;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class StationEstimateMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<Station, StationEstimateView>().Map(view => view.StationId, station => station.Id).Map(view => view.QueuedMinutes, station => station.QueuedMinutes());
  }
}
