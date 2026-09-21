using ErrorOr;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class CatalogItem
  {
    public static Error FestivalNotFound(Guid festivalId)
    {
      return NotFound("FestivalNotFound",
                      $"The festival {festivalId} the article list was asked for does not exist.");
    }

    public static Error ItemNotFound(Guid itemId)
    {
      return NotFound("ItemNotFound",
                      $"The catalog article {itemId} does not exist.");
    }

    public static Error NameTaken(string name)
    {
      return Conflict("admin.itemNameTaken",
                      $"Another catalog article already carries the name {name}.",
                      new Dictionary<string, object>
                      {
                        [MetadataKeys.ProblemCode] = "ItemNameTaken"
                      });
    }

    public static Error ProductionMinutesOutOfRange(double productionMinutes)
    {
      return BadRequest("catalog.productionMinutesOutOfRange",
                        $"The preparation time {productionMinutes} is not one the article form produces, which accepts 0 to 600 minutes with at most one decimal place, so this call did not come from that screen.",
                        new Dictionary<string, object>
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed
                        });
    }

    public static Error CategoryUnknown(Guid? categoryId)
    {
      return UnprocessableEntity("admin.itemCategoryUnknown",
                                 $"The article names the category {categoryId}, which does not exist.",
                                 new Dictionary<string, object>
                                 {
                                   [MetadataKeys.ProblemCode] = ProblemCodes.UnprocessableEntity
                                 });
    }

    public static Error CategoryIsSwitchedOff(Guid categoryId)
    {
      return UnprocessableEntity("admin.itemCategoryIsOff",
                                 $"The category {categoryId} is switched off, so an article inside it cannot be switched on.",
                                 new Dictionary<string, object>
                                 {
                                   [MetadataKeys.ProblemCode] = ProblemCodes.UnprocessableEntity
                                 });
    }

    public static Error ItemIsOnTheRunningFestivalsMenu(Guid itemId)
    {
      return Conflict("admin.itemIsOnTheRunningFestivalsMenu",
                      $"The article {itemId} is on the menu of the festival running right now.",
                      new Dictionary<string, object>
                      {
                        [MetadataKeys.ProblemCode] = "ItemIsOnTheRunningFestivalsMenu"
                      });
    }
  }
}
