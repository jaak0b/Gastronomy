using ErrorOr;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class Ingredients
  {
    public static Error IngredientNotFound(Guid ingredientId)
    {
      return NotFound("IngredientNotFound", $"The ingredient {ingredientId} does not exist.");
    }

    public static Error ItemNotFound(Guid itemId)
    {
      return NotFound("ItemNotFound", $"The catalog article {itemId} whose recipe was to change does not exist.");
    }

    public static Error FestivalNotFound(Guid festivalId)
    {
      return NotFound("FestivalNotFound", $"The festival {festivalId} whose stock was asked for does not exist.");
    }

    public static Error RecipeLineNotFound(Guid itemId, Guid ingredientId)
    {
      return NotFound("RecipeLineNotFound", $"The catalog article {itemId} does not use the ingredient {ingredientId}, so there was nothing to remove.");
    }

    public static Error StockRowNotFound(Guid festivalId, Guid ingredientId)
    {
      return NotFound("StockRowNotFound", $"The festival {festivalId} holds no stock row for the ingredient {ingredientId}.");
    }

    public static Error NameTaken(string name)
    {
      return BadRequest(RefusalMessageKeys.AdminIngredientNameTaken, $"Another ingredient already carries the name {name}, compared without regard to case.", new Dictionary<string, object> { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }

    public static Error NameBelongsToDeactivatedIngredient(string deactivatedIngredientName)
    {
      return BadRequest(RefusalMessageKeys.AdminIngredientNameBelongsToDeactivatedIngredient,
                        $"The deactivated ingredient {deactivatedIngredientName} already carries the name, compared without regard to case.",
                        new Dictionary<string, object>
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed,
                          [MetadataKeys.Name] = deactivatedIngredientName
                        });
    }

    public static Error AmountInvalid(double amount)
    {
      return BadRequest(RefusalMessageKeys.AdminIngredientAmountInvalid, $"The recipe amount {amount} is not a number above 0.", new Dictionary<string, object> { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }

    public static Error StockInvalid(double availableAmount)
    {
      return BadRequest(RefusalMessageKeys.AdminIngredientStockInvalid, $"The available amount {availableAmount} is not a number of at least 0.", new Dictionary<string, object> { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }

    public static Error UsedByActiveItems(Guid ingredientId, IReadOnlyList<string> activeItemNames)
    {
      return Conflict(RefusalMessageKeys.ActionFailed,
                      $"The ingredient {ingredientId} stays switched on because the active articles {string.Join(", ", activeItemNames)} still use it in their recipes. The ingredients page disables the delete control while an active article uses the ingredient, so this call did not come from that screen.",
                      new Dictionary<string, object> { [MetadataKeys.ProblemCode] = "IngredientUsedByActiveItems" });
    }

    public static Error DeactivatedIngredientCannotJoinRecipe(Guid itemId, Guid ingredientId)
    {
      return Conflict(RefusalMessageKeys.ActionFailed,
                      $"The ingredient {ingredientId} is deactivated, so it was not added to the recipe of the article {itemId}. The recipe dialog never sends a deactivated ingredient, so this call did not come from that screen.",
                      new Dictionary<string, object> { [MetadataKeys.ProblemCode] = "DeactivatedIngredientCannotJoinRecipe" });
    }

    public static Error UnitMissingOrUnknown(Guid? unitId)
    {
      return BadRequest(RefusalMessageKeys.ActionFailed,
                        $"The request names no unit, or the unit {unitId} does not exist. The admin screens let an ingredient choose only an existing unit, so this call did not come from them.",
                        new Dictionary<string, object> { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }
  }
}
