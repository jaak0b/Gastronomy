using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Endpoints;

public sealed class AdminItemHandler
{
  private readonly SavedChangeAnnouncement _announcement;
  private readonly CatalogChangeAnnouncer _announcer;
  private readonly ILogger<AdminItemHandler> _logger;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly CatalogItemAdministrationService _service;

  public AdminItemHandler(CatalogItemAdministrationService service,
                          CatalogChangeAnnouncer announcer,
                          SavedChangeAnnouncement announcement,
                          ResultEnvelope resultEnvelope,
                          ILogger<AdminItemHandler> logger)
  {
    _service = service;
    _announcer = announcer;
    _announcement = announcement;
    _resultEnvelope = resultEnvelope;
    _logger = logger;
  }

  public async Task<IResult> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    Result<IReadOnlyList<AdministeredCatalogItem>, CatalogItemAdministrationFailure> listed =
      await _service.ListAsync(festivalId, cancellationToken);

    if (!listed.IsSuccess)
    {
      return RefusalFor(listed.Failure);
    }

    return Results.Ok(new AdminItemListView([.. listed.Value.Select(BuildItemView)]));
  }

  public async Task<IResult> CreateAsync(SaveItemRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<Guid, CatalogItemAdministrationFailure> created =
      await _service.CreateAsync(BuildSaveRequest(request), cancellationToken);

    return await AnsweredAsync(created,
                               itemId => Results.Json(new SavedItemView(itemId),
                                                      statusCode: StatusCodes.Status201Created));
  }

  public async Task<IResult> UpdateAsync(Guid itemId, SaveItemRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<Guid, CatalogItemAdministrationFailure> updated =
      await _service.UpdateAsync(itemId, BuildSaveRequest(request), cancellationToken);

    return await AnsweredAsync(updated, savedItemId => Results.Ok(new SavedItemView(savedItemId)));
  }

  public async Task<IResult> ActivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    Result<Guid, CatalogItemAdministrationFailure> switchedOn = await _service.ActivateAsync(itemId, cancellationToken);

    return await AnsweredAsync(switchedOn, savedItemId => Results.Ok(new SavedItemView(savedItemId)));
  }

  public async Task<IResult> DeactivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    Result<Guid, CatalogItemAdministrationFailure> switchedOff =
      await _service.DeactivateAsync(itemId, cancellationToken);

    return await AnsweredAsync(switchedOff, savedItemId => Results.Ok(new SavedItemView(savedItemId)));
  }

  private SaveCatalogItemRequest BuildSaveRequest(SaveItemRequest request)
  {
    return new()
           {
             Name = request.Name,
             CategoryId = request.CategoryId,
             SortOrder = request.SortOrder,
             ProductionMinutes = request.ProductionMinutes,
             IsQueueIndependent = request.IsQueueIndependent
           };
  }

  private AdminItemView BuildItemView(AdministeredCatalogItem item)
  {
    return new(item.ItemId,
               item.Name,
               item.CategoryId,
               item.SortOrder,
               item.IsActive,
               item.ProductionMinutes,
               item.IsQueueIndependent,
               item.AtTheFestival is { } atTheFestival
                 ? new AdminItemAtFestivalView(atTheFestival.PriceCents,
                                               atTheFestival.IsAvailable,
                                               atTheFestival.StationIds)
                 : null);
  }

  private async Task<IResult> AnsweredAsync(Result<Guid, CatalogItemAdministrationFailure> written,
                                            Func<Guid, IResult> buildResponse)
  {
    if (!written.IsSuccess)
    {
      return RefusalFor(written.Failure);
    }

    await _announcement.TellTheDevicesWithoutFailingTheSavedChangeAsync(_announcer.AnnounceAsync);

    return buildResponse(written.Value);
  }

  private IResult RefusalFor(CatalogItemAdministrationFailure failure)
  {
    return failure.Reason switch
           {
             CatalogItemAdministrationFailureReason.FestivalNotFound => Results.NotFound(),
             CatalogItemAdministrationFailureReason.ItemNotFound => Results.NotFound(),
             CatalogItemAdministrationFailureReason.NameMissing =>
               _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                       "ValidationFailed",
                                       "admin.itemNameMissing"),
             CatalogItemAdministrationFailureReason.NameTaken =>
               _resultEnvelope.Problem(StatusCodes.Status409Conflict, "ItemNameTaken", "admin.itemNameTaken"),
             CatalogItemAdministrationFailureReason.ProductionMinutesOutOfRange =>
               RefusedProductionMinutes(failure.OffendingProductionMinutes),
             CatalogItemAdministrationFailureReason.CategoryUnknown =>
               _resultEnvelope.Problem(StatusCodes.Status422UnprocessableEntity,
                                       "UnprocessableEntity",
                                       "admin.itemCategoryUnknown"),
             CatalogItemAdministrationFailureReason.CategoryIsSwitchedOff =>
               _resultEnvelope.Problem(StatusCodes.Status422UnprocessableEntity,
                                       "UnprocessableEntity",
                                       "admin.itemCategoryIsOff"),
             CatalogItemAdministrationFailureReason.ItemIsOnTheRunningFestivalsMenu =>
               _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                       "ItemIsOnTheRunningFestivalsMenu",
                                       "admin.itemIsOnTheRunningFestivalsMenu"),
             _ => new UnreachableCase().Throw<IResult>(failure.Reason)
           };
  }

  private IResult RefusedProductionMinutes(double? offendingProductionMinutes)
  {
    _logger.LogWarning("An item was refused because its preparation time {ProductionMinutes} is not one the item form produces, which accepts 0 to 600 minutes with at most one decimal place, so this call did not come from that screen.",
                       offendingProductionMinutes);

    return _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                   "ValidationFailed",
                                   "catalog.productionMinutesOutOfRange");
  }
}
