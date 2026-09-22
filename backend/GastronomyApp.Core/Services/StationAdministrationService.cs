using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class StationAdministrationService
{
  private readonly IAfterCommitActions _afterCommitActions;
  private readonly IStationsChangeAnnouncer _announcer;
  private readonly IFestivalRepository _festivalRepository;
  private readonly IFestivalStationRepository _festivalStationRepository;
  private readonly ItemOrderability _orderability;
  private readonly IStationRepository _repository;
  private readonly DeviceOwnerRetirement _retirement;
  private readonly RunningFestivalLookup _runningFestival;

  public StationAdministrationService(IStationRepository repository,
                                      IFestivalRepository festivalRepository,
                                      IFestivalStationRepository festivalStationRepository,
                                      DeviceOwnerRetirement retirement,
                                      IStationsChangeAnnouncer announcer,
                                      IAfterCommitActions afterCommitActions,
                                      ItemOrderability orderability,
                                      RunningFestivalLookup runningFestival)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
    _festivalStationRepository = festivalStationRepository;
    _retirement = retirement;
    _announcer = announcer;
    _afterCommitActions = afterCommitActions;
    _orderability = orderability;
    _runningFestival = runningFestival;
  }

  public async Task<ErrorOr<IReadOnlyList<Station>>> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    if (festivalId is { } askedFestivalId && !await _festivalRepository.ExistsAsync(askedFestivalId, cancellationToken))
      return Refusal.Station.FestivalNotFound(askedFestivalId);

    IReadOnlyList<Station> stations = await _repository.FindAllAsync(festivalId, cancellationToken);

    return stations.ToErrorOr();
  }

  public async Task<ErrorOr<Station>> CreateAsync(string? name, int sortOrder, CancellationToken cancellationToken)
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
    await _afterCommitActions.RunWhenCommittedAsync(announcementCancellationToken => _announcer.AnnounceStationsChangedAsync(created.Id, announcementCancellationToken), cancellationToken);

    return created;
  }

  public async Task<ErrorOr<Station>> UpdateAsync(Guid stationId, string? name, int sortOrder, CancellationToken cancellationToken)
  {
    var station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
      return Refusal.Station.StationNotFound(stationId);

    station.Name = name!;
    station.SortOrder = sortOrder;
    await _repository.SaveChangesAsync(cancellationToken);
    await _afterCommitActions.RunWhenCommittedAsync(announcementCancellationToken => _announcer.AnnounceStationsChangedAsync(station.Id, announcementCancellationToken), cancellationToken);

    return station;
  }

  public async Task<ErrorOr<Station>> ActivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
      return Refusal.Station.StationNotFound(stationId);

    station.IsActive = true;
    await _repository.SaveChangesAsync(cancellationToken);
    await _afterCommitActions.RunWhenCommittedAsync(announcementCancellationToken => _announcer.AnnounceStationsChangedAsync(station.Id, announcementCancellationToken), cancellationToken);

    return station;
  }

  public async Task<ErrorOr<Station>> DeactivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
      return Refusal.Station.StationNotFound(stationId);

    var runningFestival = await _runningFestival.FindAsync(cancellationToken);

    if (runningFestival is not null && await _festivalStationRepository.CountUnfulfilledItemsAsync(runningFestival.Id, stationId, cancellationToken) > 0)
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
    await _afterCommitActions.RunWhenCommittedAsync(announcementCancellationToken => _announcer.AnnounceStationsChangedAsync(station.Id, announcementCancellationToken), cancellationToken);

    return station;
  }
}
