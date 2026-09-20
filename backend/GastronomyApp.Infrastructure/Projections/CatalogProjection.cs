using GastronomyApp.Core.Entities;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Infrastructure.QueryRows;
using Mapster;

namespace GastronomyApp.Infrastructure.Projections;

public sealed class CatalogProjection : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<CatalogCategory, CatalogCategoryRow>().Map(row => row.CategoryId, category => category.Id);

    config.NewConfig<Station, CatalogStationRow>().Map(row => row.StationId, station => station.Id);

    config.NewConfig<FestivalMenuItemRow, CatalogItemRow>()
          .Map(row => row.ItemId, source => source.Item.Id)
          .Map(row => row.CategoryId, source => source.Item.CategoryId)
          .Map(row => row.Name, source => source.Item.Name)
          .Map(row => row.PriceCents, source => source.MenuRow.PriceCents)
          .Map(row => row.SortOrder, source => source.Item.SortOrder)
          .Map(row => row.IsAvailable, source => source.MenuRow.IsAvailable)
          .Map(row => row.ProductionMinutes, source => source.Item.ProductionMinutes)
          .Map(row => row.IsQueueIndependent, source => source.Item.IsQueueIndependent);
  }
}
