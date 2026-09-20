using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Requests;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.ReadModels;

public sealed record ResolvedOrderItem
{
  public required OrderAcceptanceItemRequest Request { get; init; }

  public required CatalogItem CatalogItem { get; init; }

  public required RoutingDecision Decision { get; init; }
}
