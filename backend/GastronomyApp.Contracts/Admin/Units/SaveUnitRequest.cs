using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Admin.Units;

public sealed record SaveUnitRequest
{
  [RequiredText(ErrorMessage = RefusalMessageKeys.AdminUnitNameMissing)]
  public required string? Name { get; init; }
}
