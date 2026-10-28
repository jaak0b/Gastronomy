using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.TablePlan;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record SaveTableRequest
{
  public required Guid AreaId { get; init; }

  [RequiredText(ErrorMessage = RefusalMessageKeys.ActionFailed)]
  public required string? Name { get; init; }

  [Required(ErrorMessage = RefusalMessageKeys.ActionFailed)]
  public required AreaShapeGeometry? Geometry { get; init; }
}
