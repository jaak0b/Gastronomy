using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
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
    Result<SavedFestivalStation, FestivalStationFailure> added = await _service.AddAsync(festivalId, stationId, cancellationToken);

    return await AnsweredAsync(added, savedStationId => Results.Ok(new SavedStationView(savedStationId)));
  }

  public async Task<IResult> RemoveAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    Result<SavedFestivalStation, FestivalStationFailure> removed = await _service.RemoveAsync(festivalId, stationId, cancellationToken);

    return await AnsweredAsync(removed, _ => Results.NoContent());
  }

  private async Task<IResult> AnsweredAsync(Result<SavedFestivalStation, FestivalStationFailure> written, Func<Guid, IResult> buildResponse)
  {
    if (!written.IsSuccess)
      return RefusalFor(written.Failure);

    if (written.Value.SomethingChanged)
      await _announcer.AnnounceAsync(written.Value.StationId);

    return buildResponse(written.Value.StationId);
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
