using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts;

public sealed record MoveCategoryRequest
{
  public required CategoryMoveDirection Direction { get; init; }
}
