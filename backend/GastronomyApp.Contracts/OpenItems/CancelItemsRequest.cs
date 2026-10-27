using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.OpenItems;

public sealed record CancelItemsRequest
{
  [Required(ErrorMessage = RefusalMessageKeys.CancellationCannotBeProcessed)]
  [MinLength(1, ErrorMessage = RefusalMessageKeys.CancellationCannotBeProcessed)]
  public required IReadOnlyList<Guid>? OrderItemIds { get; init; }
}
