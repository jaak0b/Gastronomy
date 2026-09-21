using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminFestivalStationHandler
{
  private readonly StationChangeAnnouncer _announcer;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly FestivalStationService _service;

  public AdminFestivalStationHandler(FestivalStationService service, StationChangeAnnouncer announcer, ResultEnvelope resultEnvelope)
  {
    _service = service;
    _announcer = announcer;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> AddAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    Result<FestivalStation?, FestivalStationFailure> added = await _service.AddAsync(festivalId, stationId, cancellationToken);

    if (!added.IsSuccess)
      return RefusalFor(added.Failure);

    if (added.Value is not null)
      await _announcer.AnnounceAsync(stationId);

    return Results.Ok(new SavedStationView(stationId));
  }

  public async Task<IResult> RemoveAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    Result<FestivalStation, FestivalStationFailure> removed = await _service.RemoveAsync(festivalId, stationId, cancellationToken);

    if (!removed.IsSuccess)
      return RefusalFor(removed.Failure);

    await _announcer.AnnounceAsync(removed.Value.StationId);

    return Results.NoContent();
  }

  private IResult RefusalFor(FestivalStationFailure failure)
  {
    return failure.Reason switch
           {
             FestivalStationFailureReason.FestivalNotFound => Results.NotFound(),
             FestivalStationFailureReason.StationNotFound => Results.NotFound(),
             FestivalStationFailureReason.StationLinkNotFound => Results.NotFound(),
             FestivalStationFailureReason.StationHasUnfulfilledItems => _resultEnvelope.Problem(StatusCodes.Status409Conflict, "StationHasOrdersAtTheFestival", "admin.stationHasOrdersAtTheFestival"),
             FestivalStationFailureReason.ItemsWouldHaveNoStation => _resultEnvelope.Problem(StatusCodes.Status409Conflict, "ItemsWouldHaveNoStation", "admin.itemsWouldHaveNoStation", new Dictionary<string, string> { ["count"] = failure.StrandedItemCount.ToString() }),
             _ => new UnreachableCase().Throw<IResult>(failure.Reason)
           };
  }
}
