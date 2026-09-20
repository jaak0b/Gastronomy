using GastronomyApp.Core.Entities;
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
  private readonly ITransactionRunner _transactionRunner;

  public StationAdministrationService(IStationRepository repository,
                                      IFestivalRepository festivalRepository,
                                      IFestivalStationRepository festivalStationRepository,
                                      DeviceOwnerRetirement retirement,
                                      ItemOrderability orderability,
                                      ITransactionRunner transactionRunner,
                                      IClock clock)
  {
    _repository = repository;
    _festivalRepository = festivalRepository;
    _festivalStationRepository = festivalStationRepository;
    _retirement = retirement;
    _orderability = orderability;
    _transactionRunner = transactionRunner;
    _clock = clock;
  }

  public async Task<Result<IReadOnlyList<AdministeredStation>, StationAdministrationFailure>> ListAsync(
    Guid? festivalId,
    CancellationToken cancellationToken)
  {
    if (festivalId is { } askedFestivalId
        && !await _festivalRepository.ExistsAsync(askedFestivalId, cancellationToken))
    {
      return Result<IReadOnlyList<AdministeredStation>, StationAdministrationFailure>
        .Failed(new() { Reason = StationAdministrationFailureReason.FestivalNotFound });
    }

    IReadOnlyList<AdministeredStation> stations =
      await _repository.FindAdministeredAsync(festivalId, _clock.UtcNow, cancellationToken);

    return Result<IReadOnlyList<AdministeredStation>, StationAdministrationFailure>.Success(stations);
  }

  public Task<Result<SavedStation, StationAdministrationFailure>> CreateAsync(SaveStationNameRequest request,
                                                                              CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return RunAsync(transactionCancellationToken => CreatedAsync(request, transactionCancellationToken),
                    cancellationToken);
  }

  public Task<Result<SavedStation, StationAdministrationFailure>> UpdateAsync(Guid stationId,
                                                                              SaveStationNameRequest request,
                                                                              CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return RunAsync(transactionCancellationToken => UpdatedAsync(stationId, request, transactionCancellationToken),
                    cancellationToken);
  }

  public Task<Result<SavedStation, StationAdministrationFailure>> ActivateAsync(Guid stationId,
                                                                                CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOnAsync(stationId, transactionCancellationToken),
                    cancellationToken);
  }

  public Task<Result<SavedStation, StationAdministrationFailure>> DeactivateAsync(Guid stationId,
                                                                                  CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOffAsync(stationId, transactionCancellationToken),
                    cancellationToken);
  }

  private async Task<Result<SavedStation, StationAdministrationFailure>> CreatedAsync(
    SaveStationNameRequest request,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return Failed(StationAdministrationFailureReason.NameMissing);
    }

    var stationId = Guid.NewGuid();

    await _repository.AddAsync(new()
                               {
                                 Id = stationId,
                                 Name = request.Name,
                                 SortOrder = request.SortOrder,
                                 IsActive = true
                               },
                               cancellationToken);

    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(stationId, true, null);
  }

  private async Task<Result<SavedStation, StationAdministrationFailure>> UpdatedAsync(
    Guid stationId,
    SaveStationNameRequest request,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return Failed(StationAdministrationFailureReason.NameMissing);
    }

    Station? station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
    {
      return Failed(StationAdministrationFailureReason.StationNotFound);
    }

    var somethingChanged = station.Name != request.Name || station.SortOrder != request.SortOrder;

    station.Name = request.Name;
    station.SortOrder = request.SortOrder;
    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(stationId, somethingChanged, null);
  }

  private async Task<Result<SavedStation, StationAdministrationFailure>> SwitchedOnAsync(
    Guid stationId,
    CancellationToken cancellationToken)
  {
    Station? station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
    {
      return Failed(StationAdministrationFailureReason.StationNotFound);
    }

    var somethingChanged = !station.IsActive;

    station.IsActive = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(stationId, somethingChanged, null);
  }

  private async Task<Result<SavedStation, StationAdministrationFailure>> SwitchedOffAsync(
    Guid stationId,
    CancellationToken cancellationToken)
  {
    Station? station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
    {
      return Failed(StationAdministrationFailureReason.StationNotFound);
    }

    Festival? runningFestival = await _festivalRepository.FindRunningAsync(_clock.UtcNow, cancellationToken);

    if (runningFestival is not null
        && await _festivalStationRepository.CountUnfulfilledItemsAsync(runningFestival.Id,
                                                                       stationId,
                                                                       cancellationToken) > 0)
    {
      return Failed(StationAdministrationFailureReason.StationHasUnfulfilledItems);
    }

    IReadOnlyList<Guid> strandedItemIds =
      await _orderability.FindItemsStrandedBySwitchingOffStationAsync(stationId, cancellationToken);

    if (strandedItemIds.Count > 0)
    {
      return Result<SavedStation, StationAdministrationFailure>
        .Failed(new()
                {
                  Reason = StationAdministrationFailureReason.ItemsWouldHaveNoStation,
                  StrandedItemCount = strandedItemIds.Count
                });
    }

    var deviceId = station.DeviceId;
    var somethingChanged = station.IsActive || station.EnrolmentInvitationId is not null;

    station.IsActive = false;
    await _retirement.WithdrawOutstandingInvitationAsync(station.EnrolmentInvitationId, cancellationToken);
    station.EnrolmentInvitationId = null;
    await _repository.SaveChangesAsync(cancellationToken);

    var revokedDeviceId = await _retirement.RevokeDeviceAsync(deviceId, cancellationToken);

    return Saved(stationId, somethingChanged, revokedDeviceId);
  }

  private Result<SavedStation, StationAdministrationFailure> Saved(Guid stationId,
                                                                    bool somethingChanged,
                                                                    Guid? revokedDeviceId)
  {
    return Result<SavedStation, StationAdministrationFailure>.Success(new(stationId,
                                                                          somethingChanged,
                                                                          revokedDeviceId));
  }

  private Result<SavedStation, StationAdministrationFailure> Failed(StationAdministrationFailureReason reason)
  {
    return Result<SavedStation, StationAdministrationFailure>.Failed(new() { Reason = reason });
  }

  private async Task<Result<SavedStation, StationAdministrationFailure>> RunAsync(
    Func<CancellationToken, Task<Result<SavedStation, StationAdministrationFailure>>> write,
    CancellationToken cancellationToken)
  {
    return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                             {
                                               Result<SavedStation, StationAdministrationFailure> written =
                                                 await write(transactionCancellationToken);

                                               return new TransactionOutcome<Result<SavedStation, StationAdministrationFailure>>
                                                      {
                                                        Value = written,
                                                        ShouldCommit = written.IsSuccess
                                                      };
                                             },
                                             cancellationToken);
  }
}
