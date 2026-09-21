using GastronomyApp.Contracts;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Api.Mapping;

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
          .Map(view => view.StationIds, menuRow => menuRow.CatalogItem.StationAssignments.Select(assignment => assignment.StationId).ToList());

    config.NewConfig<Festival, RunningFestivalView>().Map(view => view.FestivalId, festival => festival.Id);

    config.NewConfig<Festival, CatalogView>()
          .Map(view => view.Festival, festival => festival)
          .Map(view => view.Categories, festival => CategoriesOnTheMenu(festival))
          .Map(view => view.Items, festival => ItemsOnTheMenu(festival))
          .Map(view => view.Stations, festival => StationsAtTheFestival(festival));
  }

  private IReadOnlyList<CatalogCategory> CategoriesOnTheMenu(Festival festival)
  {
    return festival.CatalogItems.Select(menuRow => menuRow.CatalogItem.Category).DistinctBy(category => category.Id).OrderBy(category => category.SortOrder).ToList();
  }

  private IReadOnlyList<FestivalCatalogItem> ItemsOnTheMenu(Festival festival)
  {
    return festival.CatalogItems.OrderBy(menuRow => menuRow.CatalogItem.SortOrder).ToList();
  }

  private IReadOnlyList<FestivalStation> StationsAtTheFestival(Festival festival)
  {
    return festival.Stations.OrderBy(link => link.Station.SortOrder).ToList();
  }
}
