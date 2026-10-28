using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record CopyTablesRequest
{
  public required Guid AreaId { get; init; }

  [Required(ErrorMessage = RefusalMessageKeys.ActionFailed)]
  public required IReadOnlyList<CopyTableRequest>? Tables { get; init; }
}
