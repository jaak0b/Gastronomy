using GastronomyApp.Contracts.Admin.Ingredients;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminIngredientMapping : IMappingRegistration
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<Ingredient, AdminIngredientView>().Map(view => view.IngredientId, ingredient => ingredient.Id);

    config.NewConfig<IngredientStockLevel, AdminFestivalIngredientView>()
          .Map(view => view.IngredientId, level => level.Stock.IngredientId)
          .Map(view => view.Name, level => level.Stock.Ingredient.Name)
          .Map(view => view.Unit, level => level.Stock.Ingredient.Unit)
          .Map(view => view.IsActive, level => level.Stock.Ingredient.IsActive)
          .Map(view => view.AvailableAmount, level => level.Stock.AvailableAmount)
          .Map(view => view.UsedAmount, level => level.UsedAmount)
          .Map(view => view.RunsOutAtUtc, level => level.RunsOutAtUtc);
  }
}
