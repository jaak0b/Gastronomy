using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class Ingredients
  {
    public static Error IngredientNotFound(Guid ingredientId)
    {
      return NotFound("IngredientNotFound",
                      $"The ingredient {ingredientId} does not exist.");
    }

    public static Error ItemNotFound(Guid itemId)
    {
      return NotFound("ItemNotFound",
                      $"The catalog article {itemId} whose recipe was to change does not exist.");
    }

    public static Error FestivalNotFound(Guid festivalId)
    {
      return NotFound("FestivalNotFound",
                      $"The festival {festivalId} whose stock was asked for does not exist.");
    }

    public static Error RecipeLineNotFound(Guid itemId, Guid ingredientId)
    {
      return NotFound("RecipeLineNotFound",
                      $"The catalog article {itemId} does not use the ingredient {ingredientId}, so there was nothing to remove.");
    }

    public static Error StockRowNotFound(Guid festivalId, Guid ingredientId)
    {
      return NotFound("StockRowNotFound",
                      $"The festival {festivalId} holds no stock row for the ingredient {ingredientId}.");
    }

    public static Error NameTaken(string name)
    {
      return BadRequest(RefusalMessageKeys.AdminIngredientNameTaken,
                        $"Another ingredient already carries the name {name}, compared without regard to case.",
                        new Dictionary<string, object>
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed
                        });
    }

    public static Error AmountInvalid(double amount)
    {
      return BadRequest(RefusalMessageKeys.AdminIngredientAmountInvalid,
                        $"The recipe amount {amount} is not a number above 0.",
                        new Dictionary<string, object>
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed
                        });
    }

    public static Error StockInvalid(double availableAmount)
    {
      return BadRequest(RefusalMessageKeys.AdminIngredientStockInvalid,
                        $"The available amount {availableAmount} is not a number of at least 0.",
                        new Dictionary<string, object>
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed
                        });
    }

    public static Error UnitUndefined(IngredientUnit? unit)
    {
      return BadRequest("errors.admin.actionFailed",
                        $"The unit {unit} is not one the ingredient form offers, so this call did not come from that screen.",
                        new Dictionary<string, object>
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed
                        });
    }
  }
}
