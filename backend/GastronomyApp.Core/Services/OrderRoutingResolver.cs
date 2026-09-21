using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class OrderRoutingResolver
{
  public Result<Station, Failure<RoutingFailureReason>> Resolve(Guid catalogItemId, IReadOnlyCollection<ItemStationAssignment> assignments, IReadOnlyCollection<Station> activeStations, Guid? chosenStationId)
  {
    ArgumentNullException.ThrowIfNull(assignments);
    ArgumentNullException.ThrowIfNull(activeStations);

    HashSet<Guid> assignedStationIds = assignments.Where(assignment => assignment.CatalogItemId == catalogItemId).Select(assignment => assignment.StationId).ToHashSet();

    List<Station> candidates = activeStations.Where(station => station.IsActive && assignedStationIds.Contains(station.Id)).OrderBy(station => station.SortOrder).ToList();

    if (candidates.Count == 0)
      return Result<Station, Failure<RoutingFailureReason>>.Failed(new() { Reason = RoutingFailureReason.ItemHasNoStation });

    if (chosenStationId is null)
    {
      if (candidates.Count > 1)
        return Result<Station, Failure<RoutingFailureReason>>.Failed(new() { Reason = RoutingFailureReason.StationRequired });

      return Result<Station, Failure<RoutingFailureReason>>.Success(candidates[0]);
    }

    var chosenId = chosenStationId.Value;

    if (!assignedStationIds.Contains(chosenId))
      return Result<Station, Failure<RoutingFailureReason>>.Failed(new() { Reason = RoutingFailureReason.StationNotAssignedToItem });

    var chosenCandidate = candidates.FirstOrDefault(candidate => candidate.Id == chosenId);

    if (chosenCandidate is null)
      return Result<Station, Failure<RoutingFailureReason>>.Failed(new() { Reason = RoutingFailureReason.ChosenStationNoLongerPreparesTheItem });

    return Result<Station, Failure<RoutingFailureReason>>.Success(chosenCandidate);
  }
}
