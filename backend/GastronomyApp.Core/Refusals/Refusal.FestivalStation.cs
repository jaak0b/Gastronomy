using System.Globalization;
using ErrorOr;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class FestivalStation
  {
    public static Error FestivalNotFound(Guid festivalId)
    {
      return NotFound("FestivalNotFound",
                      $"The festival {festivalId} whose stations were to change does not exist.");
    }

    public static Error StationNotFound(Guid stationId)
    {
      return NotFound("StationNotFound",
                      $"The station {stationId} does not exist.");
    }

    public static Error StationLinkNotFound(Guid festivalId, Guid stationId)
    {
      return NotFound("StationLinkNotFound",
                      $"The station {stationId} does not take part in the festival {festivalId}.");
    }

    public static Error StationHasUnfulfilledItems(Guid festivalId, Guid stationId)
    {
      return Conflict("admin.stationHasOrdersAtTheFestival",
                      $"The station {stationId} still holds items nobody has handed out at the running festival {festivalId}.",
                      new Dictionary<string, object>
                      {
                        [MetadataKeys.ProblemCode] = "StationHasOrdersAtTheFestival"
                      });
    }

    public static Error ItemsWouldHaveNoStation(int strandedItemCount)
    {
      return Conflict("admin.itemsWouldHaveNoStation",
                      $"Removing the station would leave {strandedItemCount} articles with nowhere to be prepared.",
                      new Dictionary<string, object>
                      {
                        [MetadataKeys.ProblemCode] = "ItemsWouldHaveNoStation",
                        [MetadataKeys.Count] = strandedItemCount.ToString(CultureInfo.InvariantCulture)
                      });
    }
  }
}
