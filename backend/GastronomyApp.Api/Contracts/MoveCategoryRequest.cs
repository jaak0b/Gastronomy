using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record MoveCategoryRequest
{
  public required CategoryMoveDirection Direction { get; init; }
}
