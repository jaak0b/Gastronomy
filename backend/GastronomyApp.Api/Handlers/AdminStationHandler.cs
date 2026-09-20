using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Requests;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminStationHandler
{
  private readonly StationChangeAnnouncer _announcer;
  private readonly IMapper _mapper;
  private readonly DeviceRevocationAnnouncer _revocationAnnouncer;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly StationAdministrationService _service;

  public AdminStationHandler(StationAdministrationService service, StationChangeAnnouncer announcer, DeviceRevocationAnnouncer revocationAnnouncer, ResultEnvelope resultEnvelope, IMapper mapper)
  {
    _service = service;
    _announcer = announcer;
    _revocationAnnouncer = revocationAnnouncer;
    _resultEnvelope = resultEnvelope;
    _mapper = mapper;
  }

  public async Task<IResult> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    Result<IReadOnlyList<AdministeredStation>, StationAdministrationFailure> listed = await _service.ListAsync(festivalId, cancellationToken);

    if (!listed.IsSuccess)
      return RefusalFor(listed.Failure);

    return Results.Ok(new AdminStationListView(_mapper.Map<IReadOnlyList<AdminStationView>>(listed.Value)));
  }

  public async Task<IResult> CreateAsync(SaveStationRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<SavedStation, StationAdministrationFailure> created = await _service.CreateAsync(_mapper.Map<SaveStationDetailsRequest>(request), cancellationToken);

    return await AnsweredAsync(created, stationId => Results.Json(new SavedStationView(stationId), statusCode: StatusCodes.Status201Created), cancellationToken);
  }

  public async Task<IResult> UpdateAsync(Guid stationId, SaveStationRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<SavedStation, StationAdministrationFailure> updated = await _service.UpdateAsync(stationId, _mapper.Map<SaveStationDetailsRequest>(request), cancellationToken);

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
