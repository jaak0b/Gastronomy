using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminFestivalMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<Festival, AdminFestivalView>()
          .Ignore(view => view.IsRunning)
          .Ignore(view => view.OrderCount)
          .Map(view => view.FestivalId, festival => festival.Id)
          .Map(view => view.StationCount, festival => festival.StationCount())
          .Map(view => view.MenuItemCount, festival => festival.MenuItemCount());
  }
}
