using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Orders;

public sealed record EstimateQuoteLine
{
  public required Guid CatalogItemId { get; init; }

  public required Guid StationId { get; init; }

  [Range(1, int.MaxValue, ErrorMessage = RefusalMessageKeys.OrderCannotBeProcessed)]
  public required int Units { get; init; }
}
