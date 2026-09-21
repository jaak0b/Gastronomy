using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.OpenItems;

public sealed record OrderSettlementLineRequest
{
  [Required(ErrorMessage = RefusalMessageKeys.SettlementCannotBeProcessed)]
  [Range(0, int.MaxValue, ErrorMessage = RefusalMessageKeys.SettlementCannotBeProcessed)]
  public required int? PaidPriceCents { get; init; }

  public string? PaymentNotice { get; init; }
}
