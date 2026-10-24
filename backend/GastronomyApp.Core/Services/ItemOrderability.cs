using GastronomyApp.Core.Ports;

namespace GastronomyApp.Core.Services;

public sealed class ItemOrderability
{
  private readonly TimeProvider _timeProvider;
  private readonly IFestivalRepository _festivalRepository;
  private readonly IItemOrderabilityRepository _repository;

  public ItemOrderability(IItemOrderabilityRepository repository, IFestivalRepository festivalRepository, TimeProvider timeProvider)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
    _timeProvider = timeProvider;
  }

  public async Task<IReadOnlyList<Guid>> FindOrderableItemIdsAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    IReadOnlyList<Guid> activeStations = await _repository.FindActiveStationIdsAtFestivalAsync(festivalId, cancellationToken);
    IReadOnlyList<Guid> itemIdsOnTheMenu = await _repository.FindActiveMenuItemIdsAsync(festivalId, cancellationToken);

    return await FindOrderableItemIdsAsync(festivalId, activeStations, itemIdsOnTheMenu, cancellationToken);
  }

  public async Task<bool> AnyOfTheseStationsPreparesAtAsync(Guid festivalId, IReadOnlyCollection<Guid> stationIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(stationIds);

    IReadOnlyList<Guid> stationsPreparing = await _repository.FindActiveStationIdsAtFestivalAsync(festivalId, cancellationToken);

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

    IReadOnlyList<Guid> activeStations = await _repository.FindActiveStationIdsAtFestivalAsync(festivalId, cancellationToken);
    IReadOnlyList<Guid> itemIdsOnTheMenu = await _repository.FindActiveMenuItemIdsAsync(festivalId, cancellationToken);
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

    IReadOnlyList<Guid> festivalIdsStillToCome = await _festivalRepository.FindIdsNotEndedAsync(_timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
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
    IReadOnlyList<Guid> itemIdsWithAStation = await _repository.FindItemIdsPreparedByAsync(festivalId, stationsPreparing, cancellationToken);

    HashSet<Guid> preparedSomewhere = itemIdsWithAStation.ToHashSet();

    return itemIdsOnTheMenu.Where(preparedSomewhere.Contains).ToList();
  }
}
