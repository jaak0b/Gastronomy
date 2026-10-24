using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record SaveAreaRequest
{
  [RequiredText(ErrorMessage = RefusalMessageKeys.AdminActionFailed)]
  public required string? Name { get; init; }
}
