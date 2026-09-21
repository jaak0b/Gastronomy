using System.Globalization;
using ErrorOr;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class Station
  {
    public static Error FestivalNotFound(Guid festivalId)
    {
      return NotFound("FestivalNotFound",
                      $"The festival {festivalId} the station list was asked for does not exist.");
    }

    public static Error StationNotFound(Guid stationId)
    {
      return NotFound("StationNotFound",
                      $"The station {stationId} does not exist.");
    }

    public static Error StationHasUnfulfilledItems(Guid stationId)
    {
      return Conflict("admin.stationHasUnfinishedItems",
                      $"The station {stationId} still holds items nobody has handed out at the running festival.",
                      new Dictionary<string, object>
                      {
                        [MetadataKeys.ProblemCode] = "StationHasUnfinishedItems"
                      });
    }

    public static Error ItemsWouldHaveNoStation(int strandedItemCount)
    {
      return Conflict("admin.itemsWouldHaveNoStation",
                      $"Switching the station off would leave {strandedItemCount} articles with nowhere to be prepared.",
                      new Dictionary<string, object>
                      {
                        [MetadataKeys.ProblemCode] = "ItemsWouldHaveNoStation",
                        [MetadataKeys.Count] = strandedItemCount.ToString(CultureInfo.InvariantCulture)
                      });
    }
  }
}
