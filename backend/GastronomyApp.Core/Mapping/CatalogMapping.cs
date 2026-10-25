using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Contracts.Catalog;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using Mapster;

namespace GastronomyApp.Core.Mapping;

public sealed class CatalogMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<CatalogCategory, CatalogCategoryView>().Map(view => view.CategoryId, category => category.Id);

    config.NewConfig<FestivalStation, CatalogStationView>().Map(view => view.Id, link => link.StationId).Map(view => view.Name, link => link.Station.Name).Map(view => view.SortOrder, link => link.Station.SortOrder);

    config.NewConfig<FestivalCatalogItem, CatalogItemView>()
          .Map(view => view.Id, menuRow => menuRow.CatalogItemId)
          .Map(view => view.CategoryId, menuRow => menuRow.CatalogItem.CategoryId)
          .Map(view => view.Name, menuRow => menuRow.CatalogItem.Name)
          .Map(view => view.SortOrder, menuRow => menuRow.CatalogItem.SortOrder)
          .Map(view => view.ProductionMinutes, menuRow => menuRow.CatalogItem.ProductionMinutes)
          .Map(view => view.IsQueueIndependent, menuRow => menuRow.CatalogItem.IsQueueIndependent)
          .Map(view => view.StationIds,
               menuRow => menuRow.CatalogItem.StationAssignments.Where(assignment => assignment.FestivalId == menuRow.FestivalId && assignment.Station.IsActive && assignment.Station.FestivalStations.Any(link => link.FestivalId == assignment.FestivalId))
                                 .Select(assignment => assignment.StationId)
                                 .ToList());

    config.NewConfig<FestivalCatalogItem, CatalogMenuEntry>().Map(entry => entry.Category, menuRow => menuRow.CatalogItem.Category).Map(entry => entry.Item, menuRow => menuRow);

    config.NewConfig<Festival, RunningFestivalView>().Map(view => view.FestivalId, festival => festival.Id);
  }
}
