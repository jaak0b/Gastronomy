using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminStationHandler
{
  private readonly StationChangeAnnouncer _announcer;
  private readonly IMapper _mapper;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly StationAdministrationService _service;

  public AdminStationHandler(StationAdministrationService service, StationChangeAnnouncer announcer, ResultEnvelope resultEnvelope, IMapper mapper)
  {
    _service = service;
    _announcer = announcer;
    _resultEnvelope = resultEnvelope;
    _mapper = mapper;
  }

  public async Task<IResult> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    Result<IReadOnlyList<Station>, StationAdministrationFailure> listed = await _service.ListAsync(festivalId, cancellationToken);

    if (!listed.IsSuccess)
      return RefusalFor(listed.Failure);

    return Results.Ok(new AdminStationListView(_mapper.Map<IReadOnlyList<AdminStationView>>(listed.Value)));
  }

  public async Task<IResult> CreateAsync(SaveStationRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<Station, StationAdministrationFailure> created = await _service.CreateAsync(request.Name, request.SortOrder, cancellationToken);

    if (!created.IsSuccess)
      return RefusalFor(created.Failure);

    await _announcer.AnnounceAsync(created.Value.Id);

    return Results.Json(_mapper.Map<AdminStationView>(created.Value), statusCode: StatusCodes.Status201Created);
  }

  public async Task<IResult> UpdateAsync(Guid stationId, SaveStationRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<Station?, StationAdministrationFailure> updated = await _service.UpdateAsync(stationId, request.Name, request.SortOrder, cancellationToken);

    return await AnsweredAsync(updated, stationId);
  }

  public async Task<IResult> ActivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    Result<Station?, StationAdministrationFailure> switchedOn = await _service.ActivateAsync(stationId, cancellationToken);

    return await AnsweredAsync(switchedOn, stationId);
  }

  public async Task<IResult> DeactivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    Result<Station?, StationAdministrationFailure> switchedOff = await _service.DeactivateAsync(stationId, cancellationToken);

    return await AnsweredAsync(switchedOff, stationId);
  }

  private async Task<IResult> AnsweredAsync(Result<Station?, StationAdministrationFailure> written, Guid stationId)
  {
    if (!written.IsSuccess)
      return RefusalFor(written.Failure);

    if (written.Value is not null)
      await _announcer.AnnounceAsync(stationId);

    return Results.Ok(new SavedStationView(stationId));
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
