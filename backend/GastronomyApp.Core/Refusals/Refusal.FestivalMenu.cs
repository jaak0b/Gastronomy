using ErrorOr;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class FestivalMenu
  {
    public static Error FestivalNotFound(Guid festivalId)
    {
      return NotFound("FestivalNotFound",
                      $"The festival {festivalId} whose menu was to change does not exist.");
    }

    public static Error CatalogItemNotFound(Guid catalogItemId)
    {
      return NotFound("ItemNotFound",
                      $"The catalog article {catalogItemId} that was to go on the menu does not exist.");
    }

    public static Error MenuRowNotFound(Guid festivalId, Guid catalogItemId)
    {
      return NotFound("MenuRowNotFound",
                      $"The article {catalogItemId} is not on the menu of the festival {festivalId}.");
    }

    public static Error StationsDoNotBelongToTheFestival(Guid festivalId, Guid catalogItemId, IReadOnlyList<Guid> stationIdsOutsideTheFestival)
    {
      return BadRequest("admin.actionFailed",
                        $"The article {catalogItemId} was not put on the menu of the festival {festivalId} because the stations {string.Join(", ", stationIdsOutsideTheFestival)} do not belong to that festival. The article screen offers only that festival's stations, so this call did not come from that screen.",
                        new Dictionary<string, object>
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed
                        });
    }

    public static Error NoStationPreparesTheItem(Guid catalogItemId)
    {
      return UnprocessableEntity("admin.itemNeedsAStation",
                                 $"No station at this festival would prepare the article {catalogItemId}.",
                                 new Dictionary<string, object>
                                 {
                                   [MetadataKeys.ProblemCode] = ProblemCodes.UnprocessableEntity
                                 });
    }

    public static Error FestivalIsRunning(Guid festivalId)
    {
      return Conflict("admin.itemStaysOnTheMenuWhileTheFestivalRuns",
                      $"The festival {festivalId} is running right now, so an article cannot leave its menu.",
                      new Dictionary<string, object>
                      {
                        [MetadataKeys.ProblemCode] = "ItemStaysOnTheMenuWhileTheFestivalRuns"
                      });
    }
  }
}
