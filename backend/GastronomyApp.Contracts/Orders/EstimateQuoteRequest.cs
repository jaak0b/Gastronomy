using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Orders;

public sealed record EstimateQuoteRequest
{
  [Required(ErrorMessage = RefusalMessageKeys.OrderCannotBeProcessed)]
  public required IReadOnlyList<EstimateQuoteLine> Lines { get; init; }
}
