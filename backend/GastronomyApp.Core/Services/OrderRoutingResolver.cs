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
                                                               ResolvedStationId = candidates[0].Id,
                                                               ChosenStationId = null,
                                                               FellBackFromStaleChoice = false
                                                             });
    }

    var chosenId = chosenStationId.Value;

    if (!assignedStationIds.Contains(chosenId))
    {
      return Result<RoutingDecision, RoutingFailure>.Failed(new() { Reason = RoutingFailureReason.StationNotAssignedToItem });
    }

    var chosenIsStillActive = candidates.Any(candidate => candidate.Id == chosenId);

    return Result<RoutingDecision, RoutingFailure>.Success(new()
                                                           {
                                                             ResolvedStationId = chosenIsStillActive ? chosenId : candidates[0].Id,
                                                             ChosenStationId = chosenId,
                                                             FellBackFromStaleChoice = !chosenIsStillActive
                                                           });
  }
}
