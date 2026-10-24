using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record MoveAreaRequest
{
  public required MoveDirection Direction { get; init; }

  public required bool ShowsHiddenAreas { get; init; }
}
