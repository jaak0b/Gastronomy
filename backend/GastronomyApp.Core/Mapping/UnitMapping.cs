using GastronomyApp.Contracts.Admin.Units;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Core.Mapping;

public sealed class UnitMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<Unit, AdminUnitView>().Map(view => view.UnitId, unit => unit.Id).Map(view => view.UsedByIngredientNames, unit => unit.Ingredients.Select(ingredient => ingredient.Name).OrderBy(ingredientName => ingredientName).ToList());

    config.NewConfig<Unit, SavedUnitView>().Map(view => view.UnitId, unit => unit.Id);
  }
}
