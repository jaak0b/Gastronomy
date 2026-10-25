using GastronomyApp.Contracts.Admin.Ingredients;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Core.Mapping;

public sealed class IngredientMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<Ingredient, AdminIngredientView>()
          .Map(view => view.IngredientId, ingredient => ingredient.Id)
          .Map(view => view.ActiveItemNames, ingredient => ingredient.CatalogItems.Where(recipeLine => recipeLine.CatalogItem.IsActive).Select(recipeLine => recipeLine.CatalogItem.Name).OrderBy(itemName => itemName).ToList());

    config.NewConfig<Ingredient, SavedIngredientView>().Map(view => view.IngredientId, ingredient => ingredient.Id);

    config.NewConfig<FestivalIngredient, AdminFestivalIngredientView>()
          .Map(view => view.Name, stock => stock.Ingredient.Name)
          .Map(view => view.UnitId, stock => stock.Ingredient.UnitId)
          .Map(view => view.UnitName, stock => stock.Ingredient.Unit.Name)
          .Map(view => view.UsedAmount, stock => 0d)
          .Map(view => view.Usage, stock => Array.Empty<IngredientUsagePoint>())
          .Map(view => view.RunsOutAtUtcSinceStart, stock => (DateTime?)null)
          .Map(view => view.RunsOutAtUtcFromLastTwoHours, stock => (DateTime?)null)
          .Map(view => view.StockWarning, stock => StockWarning.None)
          .Map(view => view.RunsOutSoonAtUtc, stock => (DateTime?)null);

    config.NewConfig<FestivalIngredient, SavedFestivalIngredientView>();
  }
}
