using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Orders;

public sealed record OrderItemRequest : IValidatableObject
{
  public required Guid CatalogItemId { get; init; }

  [Range(0, int.MaxValue, ErrorMessage = RefusalMessageKeys.OrderCannotBeProcessed)]
  public required int UnitPriceCents { get; init; }

  public string? Note { get; init; }

  public Guid? StationId { get; init; }

  public OrderSettlementLineRequest? Settlement { get; init; }

  public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
  {
    if (Settlement?.PaidPriceCents is not { } paidPriceCents)
      yield break;

    if (paidPriceCents < UnitPriceCents && string.IsNullOrWhiteSpace(Settlement.PaymentNotice))
      yield return new(RefusalMessageKeys.SettlementCannotBeProcessed, [nameof(Settlement)]);
  }
}
