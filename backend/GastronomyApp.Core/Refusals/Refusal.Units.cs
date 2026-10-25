using ErrorOr;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class Units
  {
    public static Error UnitNotFound(Guid unitId)
    {
      return NotFound("UnitNotFound", $"The unit {unitId} does not exist.");
    }

    public static Error NameTaken(string name)
    {
      return BadRequest(RefusalMessageKeys.AdminUnitNameTaken,
                        $"Another unit already carries the name {name}, compared without regard to case.",
                        new Dictionary<string, object>
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed,
                          [MetadataKeys.Name] = name
                        });
    }

    public static Error NameBelongsToDeactivatedUnit(string deactivatedUnitName)
    {
      return BadRequest(RefusalMessageKeys.AdminUnitNameBelongsToDeactivatedUnit,
                        $"The deactivated unit {deactivatedUnitName} already carries the name, compared without regard to case.",
                        new Dictionary<string, object>
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed,
                          [MetadataKeys.Name] = deactivatedUnitName
                        });
    }

    public static Error UsedByIngredients(Guid unitId, IReadOnlyList<string> ingredientNames)
    {
      return Conflict(RefusalMessageKeys.AdminActionFailed,
                      $"The unit {unitId} stays switched on because the ingredients {string.Join(", ", ingredientNames)} still use it. The units page disables the delete control while an ingredient uses the unit, so this call did not come from that screen.",
                      new Dictionary<string, object> { [MetadataKeys.ProblemCode] = "UnitUsedByIngredients" });
    }
  }
}
