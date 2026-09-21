using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

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

  public async Task<Result<IReadOnlyList<Station>, StationAdministrationFailure>> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    if (festivalId is { } askedFestivalId && !await _festivalRepository.ExistsAsync(askedFestivalId, cancellationToken))
      return Failed<IReadOnlyList<Station>>(StationAdministrationFailureReason.FestivalNotFound);

    IReadOnlyList<Station> stations = await _repository.FindAllAsync(festivalId, cancellationToken);

    return Result<IReadOnlyList<Station>, StationAdministrationFailure>.Success(stations);
  }

  public Task<Result<Station, StationAdministrationFailure>> CreateAsync(string? name, int sortOrder, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => CreatedAsync(name, sortOrder, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<Station?, StationAdministrationFailure>> UpdateAsync(Guid stationId, string? name, int sortOrder, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => UpdatedAsync(stationId, name, sortOrder, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<Station?, StationAdministrationFailure>> ActivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOnAsync(stationId, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<Station?, StationAdministrationFailure>> DeactivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOffAsync(stationId, transactionCancellationToken), cancellationToken);
  }

  private async Task<Result<Station, StationAdministrationFailure>> CreatedAsync(string? name, int sortOrder, CancellationToken cancellationToken)
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

    return Result<Station, StationAdministrationFailure>.Success(created);
  }

  private async Task<Result<Station?, StationAdministrationFailure>> UpdatedAsync(Guid stationId, string? name, int sortOrder, CancellationToken cancellationToken)
  {
    var station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
      return Failed<Station?>(StationAdministrationFailureReason.StationNotFound);

    var somethingChanged = station.Name != name || station.SortOrder != sortOrder;

    station.Name = name!;
    station.SortOrder = sortOrder;
    await _repository.SaveChangesAsync(cancellationToken);

    return Changed(station, somethingChanged);
  }

  private async Task<Result<Station?, StationAdministrationFailure>> SwitchedOnAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
      return Failed<Station?>(StationAdministrationFailureReason.StationNotFound);

    var somethingChanged = !station.IsActive;

    station.IsActive = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return Changed(station, somethingChanged);
  }

  private async Task<Result<Station?, StationAdministrationFailure>> SwitchedOffAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var station = await _repository.FindByIdAsync(stationId, cancellationToken);

    if (station is null)
      return Failed<Station?>(StationAdministrationFailureReason.StationNotFound);

    var runningFestival = await _runningFestival.FindAsync(cancellationToken);

    if (runningFestival is not null && await _festivalStationRepository.CountUnfulfilledItemsAsync(runningFestival.Id, stationId, cancellationToken) > 0)
      return Failed<Station?>(StationAdministrationFailureReason.StationHasUnfulfilledItems);

    IReadOnlyList<Guid> strandedItemIds = await _orderability.FindItemsStrandedBySwitchingOffStationAsync(stationId, cancellationToken);

    if (strandedItemIds.Count > 0)
    {
      return Result<Station?, StationAdministrationFailure>.Failed(new()
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

    await _retirement.RevokeDeviceAsync(deviceId, cancellationToken);

    return Changed(station, somethingChanged);
  }

  private Result<Station?, StationAdministrationFailure> Changed(Station station, bool somethingChanged)
  {
    if (!somethingChanged)
      return Result<Station?, StationAdministrationFailure>.Success(null);

    return Result<Station?, StationAdministrationFailure>.Success(station);
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
