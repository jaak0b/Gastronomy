using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class StationAdministrationService
{
  private readonly IClock _clock;
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
                                      ITransactionRunner transactionRunner,
                                      IClock clock)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
    _festivalStationRepository = festivalStationRepository;
    _retirement = retirement;
    _orderability = orderability;
    _runningFestival = runningFestival;
    _transactionRunner = transactionRunner;
    _clock = clock;
  }

  public async Task<Result<IReadOnlyList<AdministeredStation>, StationAdministrationFailure>> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    if (festivalId is { } askedFestivalId && !await _festivalRepository.ExistsAsync(askedFestivalId, cancellationToken))
      return Result<IReadOnlyList<AdministeredStation>, StationAdministrationFailure>.Failed(new() { Reason = StationAdministrationFailureReason.FestivalNotFound });

    IReadOnlyList<AdministeredStation> stations = await _repository.FindAdministeredAsync(festivalId, _clock.UtcNow, cancellationToken);

    return Result<IReadOnlyList<AdministeredStation>, StationAdministrationFailure>.Success(stations);
  }

  public Task<Result<AdministeredStation, StationAdministrationFailure>> CreateAsync(string? name, int sortOrder, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => CreatedAsync(name, sortOrder, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<SavedStation, StationAdministrationFailure>> UpdateAsync(Guid stationId, string? name, int sortOrder, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => UpdatedAsync(stationId, name, sortOrder, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<SavedStation, StationAdministrationFailure>> ActivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOnAsync(stationId, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<SavedStation, StationAdministrationFailure>> DeactivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOffAsync(stationId, transactionCancellationToken), cancellationToken);
  }

  private async Task<Result<AdministeredStation, StationAdministrationFailure>> CreatedAsync(string? name, int sortOrder, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(name))
      return Failed<AdministeredStation>(StationAdministrationFailureReason.NameMissing);

    var stationId = Guid.NewGuid();

    await _repository.AddAsync(new()
                               {
                                 Id = stationId,
                                 Name = name,
                                 SortOrder = sortOrder,
                                 IsActive = true
                               },
                               cancellationToken);

    await _repository.SaveChangesAsync(cancellationToken);

    return Result<AdministeredStation, StationAdministrationFailure>.Success(new(stationId, name, sortOrder, true, false, null, false, false));
  }

  private async Task<Result<SavedStation, StationAdministrationFailure>> UpdatedAsync(Guid stationId, string? name, int sortOrder, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(name))
      return Failed<SavedStation>(StationAdministrationFailureReason.NameMissing);

    var station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
      return Failed<SavedStation>(StationAdministrationFailureReason.StationNotFound);

    var somethingChanged = station.Name != name || station.SortOrder != sortOrder;

    station.Name = name;
    station.SortOrder = sortOrder;
    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(stationId, somethingChanged, null);
  }

  private async Task<Result<SavedStation, StationAdministrationFailure>> SwitchedOnAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
      return Failed<SavedStation>(StationAdministrationFailureReason.StationNotFound);

    var somethingChanged = !station.IsActive;

    station.IsActive = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(stationId, somethingChanged, null);
  }

  private async Task<Result<SavedStation, StationAdministrationFailure>> SwitchedOffAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
      return Failed<SavedStation>(StationAdministrationFailureReason.StationNotFound);

    var runningFestival = await _runningFestival.FindAsync(cancellationToken);

    if (runningFestival is not null && await _festivalStationRepository.CountUnfulfilledItemsAsync(runningFestival.Id, stationId, cancellationToken) > 0)
      return Failed<SavedStation>(StationAdministrationFailureReason.StationHasUnfulfilledItems);

    IReadOnlyList<Guid> strandedItemIds = await _orderability.FindItemsStrandedBySwitchingOffStationAsync(stationId, cancellationToken);

    if (strandedItemIds.Count > 0)
    {
      return Result<SavedStation, StationAdministrationFailure>.Failed(new()
                                                                       {
                                                                         Reason = StationAdministrationFailureReason.ItemsWouldHaveNoStation,
                                                                         StrandedItemCount = strandedItemIds.Count
                                                                       });
    }

    Guid? deviceId = station.DeviceId;
    var somethingChanged = station.IsActive || station.EnrolmentInvitationId is not null;

    station.IsActive = false;
    await _retirement.WithdrawOutstandingInvitationAsync(station.EnrolmentInvitationId, cancellationToken);
    station.EnrolmentInvitationId = null;
    await _repository.SaveChangesAsync(cancellationToken);

    Guid? revokedDeviceId = await _retirement.RevokeDeviceAsync(deviceId, cancellationToken);

    return Saved(stationId, somethingChanged, revokedDeviceId);
  }

  private Result<SavedStation, StationAdministrationFailure> Saved(Guid stationId, bool somethingChanged, Guid? revokedDeviceId)
  {
    return Result<SavedStation, StationAdministrationFailure>.Success(new(stationId, somethingChanged, revokedDeviceId));
  }

  private Result<TValue, StationAdministrationFailure> Failed<TValue>(StationAdministrationFailureReason reason)
  {
    return Result<TValue, StationAdministrationFailure>.Failed(new() { Reason = reason });
  }

  private async Task<Result<TValue, StationAdministrationFailure>> RunAsync<TValue>(Func<CancellationToken, Task<Result<TValue, StationAdministrationFailure>>> write, CancellationToken cancellationToken)
  {
    return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                             {
                                               Result<TValue, StationAdministrationFailure> written = await write(transactionCancellationToken);

                                               return new TransactionOutcome<Result<TValue, StationAdministrationFailure>>
                                                      {
                                                        Value = written,
                                                        ShouldCommit = written.IsSuccess
                                                      };
                                             },
                                             cancellationToken);
  }
}
