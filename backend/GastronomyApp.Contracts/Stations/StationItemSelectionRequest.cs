using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Stations;

public sealed record StationItemSelectionRequest
{
  [Required(ErrorMessage = RefusalMessageKeys.StationNoItemsSelected)]
  [MinLength(1, ErrorMessage = RefusalMessageKeys.StationNoItemsSelected)]
  public required IReadOnlyList<Guid>? OrderItemIds { get; init; }
}
