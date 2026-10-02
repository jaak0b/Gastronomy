using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Admin.Ingredients;

public sealed record SaveIngredientRequest
{
  [RequiredText(ErrorMessage = RefusalMessageKeys.AdminIngredientNameMissing)]
  public required string? Name { get; init; }

  public required IngredientUnit? Unit { get; init; }
}
