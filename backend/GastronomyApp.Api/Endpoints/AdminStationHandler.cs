using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Endpoints;

public sealed class AdminStationHandler
{
  private readonly StationChangeAnnouncer _announcer;
  private readonly DeviceRevocationAnnouncer _revocationAnnouncer;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly StationAdministrationService _service;

  public AdminStationHandler(StationAdministrationService service, StationChangeAnnouncer announcer, DeviceRevocationAnnouncer revocationAnnouncer, ResultEnvelope resultEnvelope)
  {
    _service = service;
    _announcer = announcer;
    _revocationAnnouncer = revocationAnnouncer;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    Result<IReadOnlyList<AdministeredStation>, StationAdministrationFailure> listed = await _service.ListAsync(festivalId, cancellationToken);

    if (!listed.IsSuccess)
      return RefusalFor(listed.Failure);

    return Results.Ok(new AdminStationListView(listed.Value.Select(BuildStationView).ToList()));
  }

  public async Task<IResult> CreateAsync(SaveStationRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<SavedStation, StationAdministrationFailure> created = await _service.CreateAsync(BuildSaveRequest(request), cancellationToken);

    return await AnsweredAsync(created, stationId => Results.Json(new SavedStationView(stationId), statusCode: StatusCodes.Status201Created), cancellationToken);
  }

  public async Task<IResult> UpdateAsync(Guid stationId, SaveStationRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<SavedStation, StationAdministrationFailure> updated = await _service.UpdateAsync(stationId, BuildSaveRequest(request), cancellationToken);

    return await AnsweredAsync(updated, savedStationId => Results.Ok(new SavedStationView(savedStationId)), cancellationToken);
  }

  public async Task<IResult> ActivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    Result<SavedStation, StationAdministrationFailure> switchedOn = await _service.ActivateAsync(stationId, cancellationToken);

    return await AnsweredAsync(switchedOn, savedStationId => Results.Ok(new SavedStationView(savedStationId)), cancellationToken);
  }

  public async Task<IResult> DeactivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    Result<SavedStation, StationAdministrationFailure> switchedOff = await _service.DeactivateAsync(stationId, cancellationToken);

    return await AnsweredAsync(switchedOff, savedStationId => Results.Ok(new SavedStationView(savedStationId)), cancellationToken);
  }

  private SaveStationDetailsRequest BuildSaveRequest(SaveStationRequest request)
  {
    return new()
           {
             Name = request.Name,
             SortOrder = request.SortOrder
           };
  }

  private AdminStationView BuildStationView(AdministeredStation station)
  {
    return new(station.StationId, station.Name, station.SortOrder, station.IsActive, station.HasDevice, station.LastSeenAtUtc, station.HasOutstandingInvitation, station.IsAtTheFestival);
  }

  private async Task<IResult> AnsweredAsync(Result<SavedStation, StationAdministrationFailure> written, Func<Guid, IResult> buildResponse, CancellationToken cancellationToken)
  {
    if (!written.IsSuccess)
      return RefusalFor(written.Failure);

    await _revocationAnnouncer.AnnounceAsync(written.Value.RevokedDeviceId, cancellationToken);

    if (written.Value.SomethingChanged)
      await _announcer.AnnounceAsync(written.Value.StationId);

    return buildResponse(written.Value.StationId);
  }

  private IResult RefusalFor(StationAdministrationFailure failure)
  {
    return failure.Reason switch
           {
             StationAdministrationFailureReason.FestivalNotFound => Results.NotFound(),
             StationAdministrationFailureReason.StationNotFound => Results.NotFound(),
             StationAdministrationFailureReason.NameMissing => _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "admin.stationNameMissing"),
             StationAdministrationFailureReason.StationHasUnfulfilledItems => _resultEnvelope.Problem(StatusCodes.Status409Conflict, "StationHasUnfinishedItems", "admin.stationHasUnfinishedItems"),
             StationAdministrationFailureReason.ItemsWouldHaveNoStation => _resultEnvelope.Problem(StatusCodes.Status409Conflict, "ItemsWouldHaveNoStation", "admin.itemsWouldHaveNoStation", new Dictionary<string, string> { ["count"] = failure.StrandedItemCount.ToString() }),
             _ => new UnreachableCase().Throw<IResult>(failure.Reason)
           };
  }
}
