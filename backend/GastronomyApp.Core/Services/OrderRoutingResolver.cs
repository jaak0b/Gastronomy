using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class OrderRoutingResolver
{
  public ErrorOr<Station> Resolve(CatalogItem catalogItem, IReadOnlyCollection<ItemStationAssignment> assignments, IReadOnlyCollection<Station> activeStations, Guid? chosenStationId)
  {
    ArgumentNullException.ThrowIfNull(catalogItem);
    ArgumentNullException.ThrowIfNull(assignments);
    ArgumentNullException.ThrowIfNull(activeStations);

    HashSet<Guid> assignedStationIds = assignments.Where(assignment => assignment.CatalogItemId == catalogItem.Id).Select(assignment => assignment.StationId).ToHashSet();

    List<Station> candidates = activeStations.Where(station => station.IsActive && assignedStationIds.Contains(station.Id)).OrderBy(station => station.SortOrder).ToList();

    if (candidates.Count == 0)
      return Refusal.Order.ItemHasNoStation(catalogItem.Id, catalogItem.Name);

    if (chosenStationId is null)
    {
      if (candidates.Count > 1)
        return Refusal.Order.StationRequired(catalogItem.Id, catalogItem.Name);

      return candidates[0];
    }

    var chosenId = chosenStationId.Value;

    if (!assignedStationIds.Contains(chosenId))
      return Refusal.Order.StationNotAssignedToItem(catalogItem.Id, catalogItem.Name);

    var chosenCandidate = candidates.FirstOrDefault(candidate => candidate.Id == chosenId);

    if (chosenCandidate is null)
      return Refusal.Order.ChosenStationNoLongerPreparesTheItem(catalogItem.Id, catalogItem.Name);

    return chosenCandidate;
  }
}
