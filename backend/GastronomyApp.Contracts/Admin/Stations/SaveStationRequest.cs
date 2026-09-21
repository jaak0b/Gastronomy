using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Admin.Stations;

public sealed record SaveStationRequest
{
  [RequiredText(ErrorMessage = RefusalMessageKeys.AdminStationNameMissing)]
  public required string? Name { get; init; }

  public required int SortOrder { get; init; }
}
