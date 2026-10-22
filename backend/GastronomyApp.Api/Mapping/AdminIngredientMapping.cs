using GastronomyApp.Contracts.Admin.Ingredients;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminIngredientMapping : IMappingRegistration
{
  private readonly IngredientService _ingredientService;

  public AdminIngredientMapping(IngredientService ingredientService)
  {
    _ingredientService = ingredientService;
  }

  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<Ingredient, AdminIngredientView>().Map(view => view.IngredientId, ingredient => ingredient.Id).Map(view => view.ActiveItemNames, ingredient => _ingredientService.ActiveItemNamesOf(ingredient));

    config.NewConfig<IngredientUsagePointResult, IngredientUsagePoint>();

    config.NewConfig<IngredientStockLevel, AdminFestivalIngredientView>()
          .Map(view => view.IngredientId, level => level.Stock.IngredientId)
          .Map(view => view.Name, level => level.Stock.Ingredient.Name)
          .Map(view => view.Unit, level => level.Stock.Ingredient.Unit)
          .Map(view => view.AvailableAmount, level => level.Stock.AvailableAmount)
          .Map(view => view.UsedAmount, level => level.UsedAmount)
          .Map(view => view.Usage, level => level.Usage)
          .Map(view => view.RunsOutAtUtcSinceStart, level => level.RunsOutAtUtcSinceStart)
          .Map(view => view.RunsOutAtUtcFromLastTwoHours, level => level.RunsOutAtUtcFromLastTwoHours)
          .Map(view => view.StockWarning, level => level.StockWarning)
          .Map(view => view.RunsOutSoonAtUtc, level => level.RunsOutSoonAtUtc);
  }
}
