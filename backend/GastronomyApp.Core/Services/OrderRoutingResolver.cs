using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class OrderRoutingResolver
{
  public Result<RoutingDecision, RoutingFailure> Resolve(Guid catalogItemId,
                                                         IReadOnlyCollection<ItemStationAssignment> assignments,
                                                         IReadOnlyCollection<Station> activeStations,
                                                         Guid? chosenStationId)
  {
    ArgumentNullException.ThrowIfNull(assignments);
    ArgumentNullException.ThrowIfNull(activeStations);

    HashSet<Guid> assignedStationIds = assignments
                                      .Where(assignment => assignment.CatalogItemId == catalogItemId)
                                      .Select(assignment => assignment.StationId)
                                      .ToHashSet();

    List<Station> candidates = activeStations
                              .Where(station => station.IsActive && assignedStationIds.Contains(station.Id))
                              .OrderBy(station => station.SortOrder)
                              .ToList();

    if (candidates.Count == 0)
    {
      return Result<RoutingDecision, RoutingFailure>.Failed(new() { Reason = RoutingFailureReason.ItemHasNoStation });
    }

    if (chosenStationId is null)
    {
      if (candidates.Count > 1)
      {
        return Result<RoutingDecision, RoutingFailure>.Failed(new() { Reason = RoutingFailureReason.StationRequired });
      }

      return Result<RoutingDecision, RoutingFailure>.Success(new()
                                                             {
                                                               ResolvedStationId = candidates[0].Id
                                                             });
    }

    var chosenId = chosenStationId.Value;

    if (!assignedStationIds.Contains(chosenId))
    {
      return Result<RoutingDecision, RoutingFailure>.Failed(new() { Reason = RoutingFailureReason.StationNotAssignedToItem });
    }

    if (!candidates.Any(candidate => candidate.Id == chosenId))
    {
      return Result<RoutingDecision, RoutingFailure>.Failed(new()
                                                           {
                                                             Reason = RoutingFailureReason.ChosenStationNoLongerPreparesTheItem
                                                           });
    }

    return Result<RoutingDecision, RoutingFailure>.Success(new()
                                                           {
                                                             ResolvedStationId = chosenId
                                                           });
  }
}
