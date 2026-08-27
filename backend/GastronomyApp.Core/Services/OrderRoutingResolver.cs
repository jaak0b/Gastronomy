using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class OrderRoutingResolver
{
    public Result<RoutingDecision, RoutingFailure> Resolve(
        Guid catalogItemId,
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
            return Result<RoutingDecision, RoutingFailure>.Failed(
                new RoutingFailure { Reason = RoutingFailureReason.ItemHasNoStation });
        }

        if (chosenStationId is null)
        {
            if (candidates.Count > 1)
            {
                return Result<RoutingDecision, RoutingFailure>.Failed(
                    new RoutingFailure { Reason = RoutingFailureReason.StationRequired });
            }

            return Result<RoutingDecision, RoutingFailure>.Success(new RoutingDecision
            {
                ResolvedStationId = candidates[0].Id,
                ChosenStationId = null,
                FellBackFromStaleChoice = false,
            });
        }

        Guid chosenId = chosenStationId.Value;

        if (!assignedStationIds.Contains(chosenId))
        {
            return Result<RoutingDecision, RoutingFailure>.Failed(
                new RoutingFailure { Reason = RoutingFailureReason.StationNotAssignedToItem });
        }

        bool chosenIsStillActive = candidates.Any(candidate => candidate.Id == chosenId);

        return Result<RoutingDecision, RoutingFailure>.Success(new RoutingDecision
        {
            ResolvedStationId = chosenIsStillActive ? chosenId : candidates[0].Id,
            ChosenStationId = chosenId,
            FellBackFromStaleChoice = !chosenIsStillActive,
        });
    }
}
