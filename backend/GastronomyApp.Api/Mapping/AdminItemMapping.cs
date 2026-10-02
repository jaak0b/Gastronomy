using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminItemMapping : IMappingRegistration
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<CatalogItem, AdminItemView>()
          .Map(view => view.ItemId, item => item.Id)
          .Map(view => view.Ingredients, item => item.Ingredients.OrderBy(recipeLine => recipeLine.Ingredient.Name).Select(recipeLine => new AdminItemIngredientView(recipeLine.IngredientId, recipeLine.Amount)).ToList())
          .Map(view => view.AtTheFestival,
               item => item.FestivalCatalogItems.FirstOrDefault() == null
                         ? null
                         : new AdminItemAtFestivalView(item.FestivalCatalogItems.FirstOrDefault()!.PriceCents, item.FestivalCatalogItems.FirstOrDefault()!.IsAvailable, item.StationAssignments.Select(assignment => assignment.StationId).ToList()));
  }
}
