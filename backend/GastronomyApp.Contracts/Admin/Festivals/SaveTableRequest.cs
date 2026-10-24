using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record SaveTableRequest
{
  public required Guid AreaId { get; init; }

  [RequiredText(ErrorMessage = RefusalMessageKeys.AdminActionFailed)]
  public required string? Name { get; init; }

  [Required(ErrorMessage = RefusalMessageKeys.AdminActionFailed)]
  public required AreaShapeGeometry? Geometry { get; init; }
}
