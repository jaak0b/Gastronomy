using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record SaveFestivalRequest
{
  [RequiredText(ErrorMessage = RefusalMessageKeys.AdminFestivalNameMissing)]
  public required string? Name { get; init; }

  public required DateTime StartsAtUtc { get; init; }

  public required DateTime EndsAtUtc { get; init; }
}
