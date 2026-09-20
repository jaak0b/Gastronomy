using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.QueryRows;

public sealed record FestivalMenuItemRow
{
  public required FestivalCatalogItem MenuRow { get; init; }

  public required CatalogItem Item { get; init; }

  public required IReadOnlyList<Guid> StationIds { get; init; }
}
