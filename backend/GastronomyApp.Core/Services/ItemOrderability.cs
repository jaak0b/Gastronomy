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

  public Task<IReadOnlyList<Guid>> FindOrderableItemIdsAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return FindOrderableItemIdsAsync(festivalId, [], cancellationToken);
  }

  public async Task<bool> AnyOfTheseStationsPreparesAtAsync(Guid festivalId, IReadOnlyCollection<Guid> stationIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(stationIds);

    IReadOnlyList<Guid> stationsPreparing = await _repository.FindActiveStationIdsAtFestivalAsync(festivalId, cancellationToken);

    return stationsPreparing.Any(stationIds.Contains);
  }

  public async Task<IReadOnlyList<Guid>> FindItemsStrandedByRemovingStationsAsync(Guid festivalId, IReadOnlyCollection<Guid> stationsNoLongerPreparing, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(stationsNoLongerPreparing);

    IReadOnlyList<Guid> asItIsNow = await FindOrderableItemIdsAsync(festivalId, [], cancellationToken);
    IReadOnlyList<Guid> afterTheChange = await FindOrderableItemIdsAsync(festivalId, stationsNoLongerPreparing, cancellationToken);

    return asItIsNow.Except(afterTheChange).ToList();
  }

  public async Task<IReadOnlyList<Guid>> FindItemsStrandedBySwitchingOffStationAsync(Guid stationId, CancellationToken cancellationToken)
  {
    IReadOnlyList<Guid> festivalIdsStillToCome = await _festivalRepository.FindIdsNotEndedAsync(_timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

    HashSet<Guid> stranded = [];

    foreach (var festivalId in festivalIdsStillToCome)
      stranded.UnionWith(await FindItemsStrandedByRemovingStationsAsync(festivalId, [stationId], cancellationToken));

    return stranded.ToList();
  }

  private async Task<IReadOnlyList<Guid>> FindOrderableItemIdsAsync(Guid festivalId, IReadOnlyCollection<Guid> stationsNoLongerPreparing, CancellationToken cancellationToken)
  {
    IReadOnlyList<Guid> activeStations = await _repository.FindActiveStationIdsAtFestivalAsync(festivalId, cancellationToken);

    List<Guid> stationsPreparing = activeStations.Where(stationId => !stationsNoLongerPreparing.Contains(stationId)).ToList();

    IReadOnlyList<Guid> itemIdsWithAStation = await _repository.FindItemIdsPreparedByAsync(festivalId, stationsPreparing, cancellationToken);

    IReadOnlyList<Guid> itemIdsOnTheMenu = await _repository.FindActiveMenuItemIdsAsync(festivalId, cancellationToken);

    HashSet<Guid> preparedSomewhere = itemIdsWithAStation.ToHashSet();

    return itemIdsOnTheMenu.Where(preparedSomewhere.Contains).ToList();
  }
}
