using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Core.Mapping;

public sealed class FestivalMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<Festival, AdminFestivalView>()
          .Map(view => view.FestivalId, festival => festival.Id)
          .Map(view => view.StationCount, festival => festival.Stations.Count)
          .Map(view => view.MenuItemCount, festival => festival.CatalogItems.Count)
          .Map(view => view.IsRunning, festival => false)
          .Map(view => view.OrderCount, festival => 0);

    config.NewConfig<Festival, SavedFestivalView>().Map(view => view.FestivalId, festival => festival.Id);

    config.NewConfig<FestivalCatalogItem, SavedItemView>().Map(view => view.ItemId, menuRow => menuRow.CatalogItemId);

    config.NewConfig<FestivalStation, SavedStationView>();
  }
}
