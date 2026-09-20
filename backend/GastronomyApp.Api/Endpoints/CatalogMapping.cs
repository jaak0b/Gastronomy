using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.ReadModels;
using Mapster;

namespace GastronomyApp.Api.Endpoints;

public sealed class CatalogMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<CatalogCategoryRow, CatalogCategoryView>();

    config.NewConfig<CatalogItemRow, CatalogItemView>().Map(view => view.Id, row => row.ItemId);

    config.NewConfig<CatalogStationRow, CatalogStationView>().Map(view => view.Id, row => row.StationId);

    config.NewConfig<CatalogAtFestival, RunningFestivalView>().Map(view => view.Name, catalog => catalog.FestivalName);

    config.NewConfig<CatalogAtFestival, CatalogView>().Map(view => view.Festival, catalog => catalog);
  }
}
