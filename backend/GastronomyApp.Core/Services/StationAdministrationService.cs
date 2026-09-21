using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class StationAdministrationService
{
  private readonly IFestivalRepository _festivalRepository;
  private readonly IFestivalStationRepository _festivalStationRepository;
  private readonly ItemOrderability _orderability;
  private readonly IStationRepository _repository;
  private readonly DeviceOwnerRetirement _retirement;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly ITransactionRunner _transactionRunner;

  public StationAdministrationService(IStationRepository repository,
                                      IFestivalRepository festivalRepository,
                                      IFestivalStationRepository festivalStationRepository,
                                      DeviceOwnerRetirement retirement,
                                      ItemOrderability orderability,
                                      RunningFestivalLookup runningFestival,
                                      ITransactionRunner transactionRunner)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
    _festivalStationRepository = festivalStationRepository;
    _retirement = retirement;
    _orderability = orderability;
    _runningFestival = runningFestival;
    _transactionRunner = transactionRunner;
  }

  public async Task<ErrorOr<IReadOnlyList<Station>>> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    if (festivalId is { } askedFestivalId && !await _festivalRepository.ExistsAsync(askedFestivalId, cancellationToken))
      return Refusal.Station.FestivalNotFound(askedFestivalId);

    IReadOnlyList<Station> stations = await _repository.FindAllAsync(festivalId, cancellationToken);

    return stations.ToErrorOr();
  }

  public Task<ErrorOr<Station>> CreateAsync(string? name, int sortOrder, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => CreatedAsync(name, sortOrder, transactionCancellationToken), cancellationToken);
  }

  public Task<ErrorOr<Station>> UpdateAsync(Guid stationId, string? name, int sortOrder, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => UpdatedAsync(stationId, name, sortOrder, transactionCancellationToken), cancellationToken);
  }

  public Task<ErrorOr<Station>> ActivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => SwitchedOnAsync(stationId, transactionCancellationToken), cancellationToken);
  }

  public Task<ErrorOr<Station>> DeactivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => SwitchedOffAsync(stationId, transactionCancellationToken), cancellationToken);
  }

  private async Task<ErrorOr<Station>> CreatedAsync(string? name, int sortOrder, CancellationToken cancellationToken)
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

    return created;
  }

  private async Task<ErrorOr<Station>> UpdatedAsync(Guid stationId, string? name, int sortOrder, CancellationToken cancellationToken)
  {
    var station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
      return Refusal.Station.StationNotFound(stationId);

    station.Name = name!;
    station.SortOrder = sortOrder;
    await _repository.SaveChangesAsync(cancellationToken);

    return station;
  }

  private async Task<ErrorOr<Station>> SwitchedOnAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
      return Refusal.Station.StationNotFound(stationId);

    station.IsActive = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return station;
  }

  private async Task<ErrorOr<Station>> SwitchedOffAsync(Guid stationId, CancellationToken cancellationToken)
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

    return station;
  }
}
