using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminItemMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<CatalogItem, AdminItemView>().Map(view => view.ItemId, item => item.Id).Map(view => view.AtTheFestival, item => AtTheFestival(item));
  }

  private AdminItemAtFestivalView? AtTheFestival(CatalogItem item)
  {
    var menuRow = item.FestivalCatalogItems.FirstOrDefault();

    if (menuRow is null)
      return null;

    return new(menuRow.PriceCents, menuRow.IsAvailable, item.StationAssignments.Select(assignment => assignment.StationId).ToList());
  }
}
