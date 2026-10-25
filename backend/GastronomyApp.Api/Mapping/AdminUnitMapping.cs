using GastronomyApp.Contracts.Admin.Units;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminUnitMapping : IMappingRegistration
{
  private readonly UnitService _unitService;

  public AdminUnitMapping(UnitService unitService)
  {
    _unitService = unitService;
  }

  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<Unit, AdminUnitView>().Map(view => view.UnitId, unit => unit.Id).Map(view => view.UsedByIngredientNames, unit => _unitService.IngredientNamesOf(unit));
  }
}
