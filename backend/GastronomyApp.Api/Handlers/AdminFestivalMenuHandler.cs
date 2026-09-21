using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts;
using GastronomyApp.Core.Entities;
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

    Result<FestivalCatalogItem, FestivalMenuFailure> putOn = await _service.PutOnTheMenuAsync(festivalId, itemId, request.PriceCents, request.StationIds, cancellationToken);

    if (!putOn.IsSuccess)
      return RefusalFor(putOn.Failure, festivalId, itemId);

    await TellTheDevicesAsync();

    return Results.Ok(new SavedItemView(putOn.Value.CatalogItemId));
  }

  public async Task<IResult> TakeOffTheMenuAsync(Guid festivalId, Guid itemId, CancellationToken cancellationToken)
  {
    Result<FestivalCatalogItem, FestivalMenuFailure> takenOff = await _service.TakeOffTheMenuAsync(festivalId, itemId, cancellationToken);

    if (!takenOff.IsSuccess)
      return RefusalFor(takenOff.Failure, festivalId, itemId);

    await TellTheDevicesAsync();

    return Results.NoContent();
  }

  public async Task<IResult> SetAvailabilityAsync(Guid festivalId, Guid itemId, SetAvailabilityRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<FestivalCatalogItem?, FestivalMenuFailure> saved = await _service.SetAvailabilityAsync(festivalId, itemId, request.IsAvailable, cancellationToken);

    if (!saved.IsSuccess)
      return RefusalFor(saved.Failure, festivalId, itemId);

    if (saved.Value is not null)
      await TellTheDevicesAsync();

    return Results.Ok(new SavedItemView(itemId));
  }

  private async Task TellTheDevicesAsync()
  {
    await _savedChangeAnnouncer.TellTheDevicesWithoutFailingTheSavedChangeAsync(_announcer.AnnounceAsync);
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
