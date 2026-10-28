using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.TablePlan;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Admin.Festivals;

public sealed record SaveStationShapeRequest
{
  public required Guid AreaId { get; init; }

  [Required(ErrorMessage = RefusalMessageKeys.ActionFailed)]
  public required AreaShapeGeometry? Geometry { get; init; }
}
