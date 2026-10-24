using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Orders;

public sealed record PlaceOrderRequest
{
  public required Guid ClientOrderId { get; init; }

  public required Guid TableId { get; init; }

  [Required(ErrorMessage = RefusalMessageKeys.OrderCannotBeProcessed)]
  [MinLength(1, ErrorMessage = RefusalMessageKeys.OrderCannotBeProcessed)]
  public required IReadOnlyList<OrderItemRequest>? Items { get; init; }

  public IReadOnlyList<OrderDeliveryModeRequest>? DeliveryModes { get; init; }
}
