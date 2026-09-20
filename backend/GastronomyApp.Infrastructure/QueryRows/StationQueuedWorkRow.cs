using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.QueryRows;

public sealed record StationQueuedWorkRow
{
  public required StationOrder StationOrder { get; init; }

  public required CatalogItem CatalogItem { get; init; }
}
