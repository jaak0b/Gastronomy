using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Queries;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class ItemOrderability
{
  private readonly IGastronomyDatabase _database;
  private readonly TimeProvider _timeProvider;

  public ItemOrderability(IGastronomyDatabase database, TimeProvider timeProvider)
  {
    _database = database;
    _timeProvider = timeProvider;
  }

  public async Task<IReadOnlyList<Guid>> FindOrderableItemIdsAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    IReadOnlyList<Guid> activeStations = await FindActiveStationIdsAsync(festivalId, cancellationToken);
    IReadOnlyList<Guid> itemIdsOnTheMenu = await FindActiveMenuItemIdsAsync(festivalId, cancellationToken);

    return await FindOrderableItemIdsAsync(festivalId, activeStations, itemIdsOnTheMenu, cancellationToken);
  }

  public async Task<bool> AnyOfTheseStationsPreparesAtAsync(Guid festivalId, IReadOnlyCollection<Guid> stationIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(stationIds);

    IReadOnlyList<Guid> stationsPreparing = await FindActiveStationIdsAsync(festivalId, cancellationToken);

    return stationsPreparing.Any(stationIds.Contains);
  }

  public async Task<IReadOnlyList<Guid>> FindItemsStrandedByRemovingStationAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> stranded = await FindItemsStrandedByRemovingEachStationAsync(festivalId, [stationId], cancellationToken);

    return stranded[stationId];
  }

  public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> FindItemsStrandedByRemovingEachStationAsync(Guid festivalId, IReadOnlyCollection<Guid> stationIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(stationIds);

    IReadOnlyList<Guid> activeStations = await FindActiveStationIdsAsync(festivalId, cancellationToken);
    IReadOnlyList<Guid> itemIdsOnTheMenu = await FindActiveMenuItemIdsAsync(festivalId, cancellationToken);
    IReadOnlyList<Guid> asItIsNow = await FindOrderableItemIdsAsync(festivalId, activeStations, itemIdsOnTheMenu, cancellationToken);
    Dictionary<Guid, IReadOnlyList<Guid>> stranded = [];

    foreach (var stationId in stationIds.Distinct())
      stranded[stationId] = activeStations.Contains(stationId) ? asItIsNow.Except(await FindOrderableItemIdsAsync(festivalId, activeStations.Where(activeStationId => activeStationId != stationId).ToList(), itemIdsOnTheMenu, cancellationToken)).ToList() : [];

    return stranded;
  }

  public async Task<IReadOnlyList<Guid>> FindItemsStrandedBySwitchingOffStationAsync(Guid stationId, CancellationToken cancellationToken)
  {
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> stranded = await FindItemsStrandedBySwitchingOffEachStationAsync([stationId], cancellationToken);

    return stranded[stationId];
  }

  public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> FindItemsStrandedBySwitchingOffEachStationAsync(IReadOnlyCollection<Guid> stationIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(stationIds);

    IReadOnlyList<Guid> festivalIdsStillToCome = await FindFestivalIdsNotEndedAsync(cancellationToken);
    Dictionary<Guid, HashSet<Guid>> stranded = stationIds.Distinct().ToDictionary(stationId => stationId, stationId => new HashSet<Guid>());

    foreach (var festivalId in festivalIdsStillToCome)
    {
      foreach (var strandedAtFestival in await FindItemsStrandedByRemovingEachStationAsync(festivalId, stationIds, cancellationToken))
        stranded[strandedAtFestival.Key].UnionWith(strandedAtFestival.Value);
    }

    return stranded.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<Guid>)entry.Value.ToList());
  }

  private async Task<IReadOnlyList<Guid>> FindOrderableItemIdsAsync(Guid festivalId, IReadOnlyCollection<Guid> stationsPreparing, IReadOnlyCollection<Guid> itemIdsOnTheMenu, CancellationToken cancellationToken)
  {
    IReadOnlyList<Guid> itemIdsWithAStation = await FindItemIdsPreparedByAsync(festivalId, stationsPreparing, cancellationToken);

    HashSet<Guid> preparedSomewhere = itemIdsWithAStation.ToHashSet();

    return itemIdsOnTheMenu.Where(preparedSomewhere.Contains).ToList();
  }

  private async Task<IReadOnlyList<Guid>> FindActiveStationIdsAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _database.FestivalStations.AsNoTracking().ActiveAt(festivalId).Select(link => link.StationId).ToListAsync(cancellationToken);
  }

  private async Task<IReadOnlyList<Guid>> FindActiveMenuItemIdsAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _database.FestivalCatalogItems.AsNoTracking().Where(menuRow => menuRow.FestivalId == festivalId && menuRow.CatalogItem.IsActive).OrderBy(menuRow => menuRow.CatalogItem.SortOrder).Select(menuRow => menuRow.CatalogItemId).ToListAsync(cancellationToken);
  }

  private async Task<IReadOnlyList<Guid>> FindItemIdsPreparedByAsync(Guid festivalId, IReadOnlyCollection<Guid> stationIds, CancellationToken cancellationToken)
  {
    List<Guid> preparing = stationIds.ToList();

    return await _database.ItemStationAssignments.AsNoTracking().Where(assignment => assignment.FestivalId == festivalId && preparing.Contains(assignment.StationId)).Select(assignment => assignment.CatalogItemId).Distinct().ToListAsync(cancellationToken);
  }

  private async Task<IReadOnlyList<Guid>> FindFestivalIdsNotEndedAsync(CancellationToken cancellationToken)
  {
    var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

    return await _database.Festivals.AsNoTracking().Where(festival => !festival.IsHidden && festival.EndsAtUtc > nowUtc).Select(festival => festival.Id).ToListAsync(cancellationToken);
  }
}
