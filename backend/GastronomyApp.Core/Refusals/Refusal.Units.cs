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

    public static Error UsedByIngredients(Guid unitId, IReadOnlyList<string> ingredientNames)
    {
      return Conflict(RefusalMessageKeys.ActionFailed,
                      $"The unit {unitId} was not deleted because the ingredients {string.Join(", ", ingredientNames)} still use it. The admin screens offer delete only for a unit no ingredient uses, so this call did not come from them.",
                      new Dictionary<string, object> { [MetadataKeys.ProblemCode] = "UnitUsedByIngredients" });
    }
  }
}
