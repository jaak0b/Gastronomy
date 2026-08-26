using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class OrderRoutingResolver
{
    public Result<RoutingDecision, RoutingFailure> Resolve(
        Guid catalogItemId,
        IReadOnlyCollection<ItemLocationAssignment> assignments,
        IReadOnlyCollection<ProductionLocation> activeLocations,
        Guid? chosenProductionLocationId)
    {
        HashSet<Guid> assignedLocationIds = assignments
            .Where(assignment => assignment.CatalogItemId == catalogItemId)
            .Select(assignment => assignment.ProductionLocationId)
            .ToHashSet();

        List<ProductionLocation> candidates = activeLocations
            .Where(location => location.IsActive && assignedLocationIds.Contains(location.Id))
            .OrderBy(location => location.SortOrder)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"The catalog item {catalogItemId} has no active production location assigned to it.");
        }

        if (chosenProductionLocationId is null)
        {
            if (candidates.Count > 1)
            {
                return Result<RoutingDecision, RoutingFailure>.Failed(
                    new RoutingFailure { Reason = RoutingFailureReason.StationRequired });
            }

            return Result<RoutingDecision, RoutingFailure>.Success(new RoutingDecision
            {
                ResolvedProductionLocationId = candidates[0].Id,
                ChosenProductionLocationId = null,
                FellBackFromStaleChoice = false,
            });
        }

        Guid chosenId = chosenProductionLocationId.Value;

        if (!assignedLocationIds.Contains(chosenId))
        {
            return Result<RoutingDecision, RoutingFailure>.Failed(
                new RoutingFailure { Reason = RoutingFailureReason.StationNotAssignedToItem });
        }

        bool chosenIsStillActive = candidates.Any(candidate => candidate.Id == chosenId);

        return Result<RoutingDecision, RoutingFailure>.Success(new RoutingDecision
        {
            ResolvedProductionLocationId = chosenIsStillActive ? chosenId : candidates[0].Id,
            ChosenProductionLocationId = chosenId,
            FellBackFromStaleChoice = !chosenIsStillActive,
        });
    }
}
