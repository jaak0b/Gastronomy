using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Orders;

public sealed record OrderItemRequest
{
  public required Guid CatalogItemId { get; init; }

  [Range(0, int.MaxValue, ErrorMessage = RefusalMessageKeys.OrderCannotBeProcessed)]
  public required int UnitPriceCents { get; init; }

  public string? Note { get; init; }

  public Guid? StationId { get; init; }
}
