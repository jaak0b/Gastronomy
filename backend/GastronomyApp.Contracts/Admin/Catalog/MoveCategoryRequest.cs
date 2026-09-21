using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.Admin.Catalog;

public sealed record MoveCategoryRequest
{
  public required CategoryMoveDirection Direction { get; init; }
}
