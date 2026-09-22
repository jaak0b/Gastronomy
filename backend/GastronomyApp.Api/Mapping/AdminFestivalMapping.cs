using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminFestivalMapping
{
  private readonly FestivalService _festivalService;

  public AdminFestivalMapping(FestivalService festivalService)
  {
    _festivalService = festivalService;
  }

  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<Festival, AdminFestivalView>()
          .Ignore(view => view.IsRunning)
          .Ignore(view => view.OrderCount)
          .Map(view => view.FestivalId, festival => festival.Id)
          .Map(view => view.StationCount, festival => _festivalService.StationCountOf(festival))
          .Map(view => view.MenuItemCount, festival => _festivalService.MenuItemCountOf(festival));
  }
}
