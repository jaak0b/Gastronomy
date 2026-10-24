using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class StationAdministrationService
{
  private readonly IFestivalRepository _festivalRepository;
  private readonly ItemsToHandOutLookup _itemsToHandOut;
  private readonly ItemOrderability _orderability;
  private readonly IStationRepository _repository;
  private readonly DeviceOwnerRetirement _retirement;

  public StationAdministrationService(IStationRepository repository, IFestivalRepository festivalRepository, DeviceOwnerRetirement retirement, ItemOrderability orderability, ItemsToHandOutLookup itemsToHandOut)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
    _retirement = retirement;
    _orderability = orderability;
    _itemsToHandOut = itemsToHandOut;
  }

  public async Task<ErrorOr<IReadOnlyList<ListedStation>>> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    if (festivalId is not { } askedFestivalId)
    {
      IReadOnlyList<Station> stations = await _repository.FindAllAsync(null, cancellationToken);
      IReadOnlyCollection<Guid> holdingItems = await _itemsToHandOut.FindStationIdsAtTheRunningFestivalAsync(cancellationToken);
      IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> strandedBySwitchingOff = await _orderability.FindItemsStrandedBySwitchingOffEachStationAsync(stations.Select(station => station.Id).ToList(), cancellationToken);

      return stations.Select(station => new ListedStation(station, holdingItems.Contains(station.Id), strandedBySwitchingOff[station.Id].Count, null)).ToList().ToErrorOr<IReadOnlyList<ListedStation>>();
    }

    var festival = await _festivalRepository.FindByIdAsync(askedFestivalId, cancellationToken);

    if (festival is null)
      return Refusal.Station.FestivalNotFound(askedFestivalId);

    IReadOnlyList<Station> festivalStations = await _repository.FindAllAsync(askedFestivalId, cancellationToken);
    IReadOnlyCollection<Guid> holdingItemsAtTheFestival = await _itemsToHandOut.FindStationIdsIfRunningAsync(festival, cancellationToken);
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> strandedByRemoving = await _orderability.FindItemsStrandedByRemovingEachStationAsync(askedFestivalId, festivalStations.Select(station => station.Id).ToList(), cancellationToken);

    return festivalStations.Select(station => new ListedStation(station, holdingItemsAtTheFestival.Contains(station.Id), null, strandedByRemoving[station.Id].Count)).ToList().ToErrorOr<IReadOnlyList<ListedStation>>();
  }

  public async Task<ErrorOr<ListedStation>> CreateAsync(string? name, int sortOrder, CancellationToken cancellationToken)
  {
    Station created = new()
                      {
                        Id = Guid.NewGuid(),
                        Name = name!,
                        SortOrder = sortOrder,
                        IsActive = true
                      };

    await _repository.AddAsync(created, cancellationToken);

    await _repository.SaveChangesAsync(cancellationToken);

    return new ListedStation(created, false, 0, 0);
  }

  public async Task<ErrorOr<Station>> UpdateAsync(Guid stationId, string? name, int sortOrder, CancellationToken cancellationToken)
  {
    var station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
      return Refusal.Station.StationNotFound(stationId);

    station.Name = name!;
    station.SortOrder = sortOrder;
    await _repository.SaveChangesAsync(cancellationToken);

    return station;
  }

  public async Task<ErrorOr<Station>> ActivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
      return Refusal.Station.StationNotFound(stationId);

    station.IsActive = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return station;
  }

  public async Task<ErrorOr<Station>> DeactivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
      return Refusal.Station.StationNotFound(stationId);

    if ((await _itemsToHandOut.FindStationIdsAtTheRunningFestivalAsync(cancellationToken)).Contains(stationId))
      return Refusal.Station.StationHasUnfulfilledItems(stationId);

    IReadOnlyList<Guid> strandedItemIds = await _orderability.FindItemsStrandedBySwitchingOffStationAsync(stationId, cancellationToken);

    if (strandedItemIds.Count > 0)
      return Refusal.Station.ItemsWouldHaveNoStation(strandedItemIds.Count);

    Guid? deviceId = station.DeviceId;

    station.IsActive = false;
    await _retirement.WithdrawOutstandingInvitationAsync(station.EnrolmentInvitationId, cancellationToken);
    station.EnrolmentInvitationId = null;
    await _repository.SaveChangesAsync(cancellationToken);

    await _retirement.RevokeDeviceAsync(deviceId, cancellationToken);

    return station;
  }
}
