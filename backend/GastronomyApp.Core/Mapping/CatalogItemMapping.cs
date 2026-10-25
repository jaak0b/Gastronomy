using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using Mapster;

namespace GastronomyApp.Core.Mapping;

public sealed class CatalogItemMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<CatalogItemIngredient, AdminItemIngredientView>().Map(view => view.Name, recipeLine => recipeLine.Ingredient.Name).Map(view => view.UnitId, recipeLine => recipeLine.Ingredient.UnitId).Map(view => view.UnitName, recipeLine => recipeLine.Ingredient.Unit.Name);

    config.NewConfig<FestivalCatalogItem, AdminItemAtFestivalView>().Map(view => view.StationIds, menuRow => menuRow.CatalogItem.StationAssignments.Where(assignment => assignment.FestivalId == menuRow.FestivalId).Select(assignment => assignment.StationId).ToList());

    config.NewConfig<FestivalCatalogItem, ItemMenuEntry>().Map(entry => entry.AtTheFestival, menuRow => menuRow);

    ItemRules(config.NewConfig<CatalogItem, AdminItemView>()).Map(view => view.AtTheFestival, item => (AdminItemAtFestivalView?)null);

    config.NewConfig<CatalogItem, SavedItemView>().Map(view => view.ItemId, item => item.Id);

    config.NewConfig<CatalogItemIngredient, SavedItemView>().Map(view => view.ItemId, recipeLine => recipeLine.CatalogItemId);
  }

  private TypeAdapterSetter<CatalogItem, AdminItemView> ItemRules(TypeAdapterSetter<CatalogItem, AdminItemView> setter)
  {
    return setter.Map(view => view.ItemId, item => item.Id).Map(view => view.Ingredients, item => item.Ingredients.OrderBy(recipeLine => recipeLine.Ingredient.Name));
  }
}
