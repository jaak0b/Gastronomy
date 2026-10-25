using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record MoveTablesRequest
{
  [Required(ErrorMessage = RefusalMessageKeys.AdminActionFailed)]
  public required IReadOnlyList<MoveTableRequest>? Tables { get; init; }
}
