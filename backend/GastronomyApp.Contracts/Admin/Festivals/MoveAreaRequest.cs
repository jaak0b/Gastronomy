using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record MoveAreaRequest
{
  public required MoveDirection Direction { get; init; }
}
