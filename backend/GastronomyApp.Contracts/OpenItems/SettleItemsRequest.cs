using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.OpenItems;

public sealed record SettleItemsRequest
{
  [Required(ErrorMessage = RefusalMessageKeys.SettlementNoItemsSelected)]
  [MinLength(1, ErrorMessage = RefusalMessageKeys.SettlementNoItemsSelected)]
  public required IReadOnlyList<SettleLineRequest>? Lines { get; init; }
}
