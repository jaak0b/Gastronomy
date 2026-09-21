using ErrorOr;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class CatalogCategory
  {
    public static Error CategoryNotFound(Guid categoryId)
    {
      return NotFound("CategoryNotFound",
                      $"The catalog category {categoryId} does not exist.");
    }

    public static Error NameTaken(string name)
    {
      return Conflict("admin.categoryNameTaken",
                      $"Another catalog category already carries the name {name}.",
                      new Dictionary<string, object>
                      {
                        [MetadataKeys.ProblemCode] = "CategoryNameTaken"
                      });
    }

    public static Error CategoryHoldsActiveItems(Guid categoryId)
    {
      return Conflict("admin.categoryHasActiveItems",
                      $"The catalog category {categoryId} still holds articles that are switched on.",
                      new Dictionary<string, object>
                      {
                        [MetadataKeys.ProblemCode] = "CategoryHasActiveItems"
                      });
    }
  }
}
