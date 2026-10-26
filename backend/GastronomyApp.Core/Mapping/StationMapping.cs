using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Core.Mapping;

public sealed class StationMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<Station, AdminStationView>()
          .Map(view => view.StationId, station => station.Id)
          .Map(view => view.HasDevice, station => station.DeviceId != null)
          .Map(view => view.IsAtFestival, station => false)
          .Map(view => view.HasItemsToHandOutAtTheRunningFestival, station => false)
          .Map(view => view.StrandedItemCountWhenSwitchedOff, station => (int?)null)
          .Map(view => view.StrandedItemCountWhenRemovedFromTheFestival, station => (int?)null);

    config.NewConfig<Station, SavedStationView>().Map(view => view.StationId, station => station.Id);
  }
}
