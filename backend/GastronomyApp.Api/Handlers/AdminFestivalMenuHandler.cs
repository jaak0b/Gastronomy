using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminFestivalMenuHandler
{
  private readonly CatalogChangeAnnouncer _announcer;
  private readonly ILogger<AdminFestivalMenuHandler> _logger;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly SavedChangeAnnouncer _savedChangeAnnouncer;
  private readonly FestivalMenuService _service;

  public AdminFestivalMenuHandler(FestivalMenuService service, CatalogChangeAnnouncer announcer, SavedChangeAnnouncer savedChangeAnnouncer, ResultEnvelope resultEnvelope, ILogger<AdminFestivalMenuHandler> logger)
  {
    _service = service;
    _announcer = announcer;
    _savedChangeAnnouncer = savedChangeAnnouncer;
    _resultEnvelope = resultEnvelope;
    _logger = logger;
  }

  public async Task<IResult> PutOnTheMenuAsync(Guid festivalId, Guid itemId, SaveFestivalItemRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<SavedFestivalMenuItem, FestivalMenuFailure> putOn = await _service.PutOnTheMenuAsync(festivalId,
                                                                                                itemId,
                                                                                                new()
                                                                                                {
                                                                                                  PriceCents = request.PriceCents,
                                                                                                  StationIds = request.StationIds
                                                                                                },
                                                                                                cancellationToken);

    return await AnsweredAsync(putOn, festivalId, itemId, savedItemId => Results.Ok(new SavedItemView(savedItemId)));
  }

  public async Task<IResult> TakeOffTheMenuAsync(Guid festivalId, Guid itemId, CancellationToken cancellationToken)
  {
    Result<SavedFestivalMenuItem, FestivalMenuFailure> takenOff = await _service.TakeOffTheMenuAsync(festivalId, itemId, cancellationToken);

    return await AnsweredAsync(takenOff, festivalId, itemId, _ => Results.NoContent());
  }

  public async Task<IResult> SetAvailabilityAsync(Guid festivalId, Guid itemId, SetAvailabilityRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<SavedFestivalMenuItem, FestivalMenuFailure> saved = await _service.SetAvailabilityAsync(festivalId, itemId, request.IsAvailable, cancellationToken);

    return await AnsweredAsync(saved, festivalId, itemId, savedItemId => Results.Ok(new SavedItemView(savedItemId)));
  }

  private async Task<IResult> AnsweredAsync(Result<SavedFestivalMenuItem, FestivalMenuFailure> written, Guid festivalId, Guid itemId, Func<Guid, IResult> buildResponse)
  {
    if (!written.IsSuccess)
      return RefusalFor(written.Failure, festivalId, itemId);

    if (written.Value.SomethingChanged)
      await _savedChangeAnnouncer.TellTheDevicesWithoutFailingTheSavedChangeAsync(_announcer.AnnounceAsync);

    return buildResponse(written.Value.CatalogItemId);
  }

  private IResult RefusalFor(FestivalMenuFailure failure, Guid festivalId, Guid itemId)
  {
    return failure.Reason switch
           {
             FestivalMenuFailureReason.FestivalNotFound => Results.NotFound(),
             FestivalMenuFailureReason.CatalogItemNotFound => Results.NotFound(),
             FestivalMenuFailureReason.MenuRowNotFound => Results.NotFound(),
             FestivalMenuFailureReason.PriceOutOfRange => _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "admin.itemPriceOutOfRange"),
             FestivalMenuFailureReason.StationsDoNotBelongToTheFestival => RefusedStationsOutsideTheFestival(failure.StationIdsOutsideTheFestival, festivalId, itemId),
             FestivalMenuFailureReason.NoStationPreparesTheItem => _resultEnvelope.Problem(StatusCodes.Status422UnprocessableEntity, "UnprocessableEntity", "admin.itemNeedsAStation"),
             FestivalMenuFailureReason.FestivalIsRunning => _resultEnvelope.Problem(StatusCodes.Status409Conflict, "ItemStaysOnTheMenuWhileTheFestivalRuns", "admin.itemStaysOnTheMenuWhileTheFestivalRuns"),
             _ => new UnreachableCase().Throw<IResult>(failure.Reason)
           };
  }

  private IResult RefusedStationsOutsideTheFestival(IReadOnlyList<Guid> stationIdsOutsideTheFestival, Guid festivalId, Guid itemId)
  {
    _logger.LogWarning("The item {ItemId} was not put on the menu of the festival {FestivalId} because the stations {StationIds} do not belong to that festival. The item screen offers only that festival's stations, so this call did not come from that screen.",
                       itemId,
                       festivalId,
                       stationIdsOutsideTheFestival);

    return _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "admin.actionFailed");
  }
}
