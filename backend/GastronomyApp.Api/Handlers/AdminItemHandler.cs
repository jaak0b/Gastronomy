using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminItemHandler
{
  private readonly CatalogChangeAnnouncer _announcer;
  private readonly ILogger<AdminItemHandler> _logger;
  private readonly IMapper _mapper;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly SavedChangeAnnouncer _savedChangeAnnouncer;
  private readonly CatalogItemAdministrationService _service;

  public AdminItemHandler(CatalogItemAdministrationService service, CatalogChangeAnnouncer announcer, SavedChangeAnnouncer savedChangeAnnouncer, ResultEnvelope resultEnvelope, ILogger<AdminItemHandler> logger, IMapper mapper)
  {
    _service = service;
    _announcer = announcer;
    _savedChangeAnnouncer = savedChangeAnnouncer;
    _resultEnvelope = resultEnvelope;
    _logger = logger;
    _mapper = mapper;
  }

  public async Task<IResult> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    Result<IReadOnlyList<AdministeredCatalogItem>, CatalogItemAdministrationFailure> listed = await _service.ListAsync(festivalId, cancellationToken);

    if (!listed.IsSuccess)
      return RefusalFor(listed.Failure);

    return Results.Ok(new AdminItemListView(listed.Value.Select(_mapper.Map<AdminItemView>).ToList()));
  }

  public async Task<IResult> CreateAsync(SaveItemRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<AdministeredCatalogItem, CatalogItemAdministrationFailure> created = await _service.CreateAsync(request.Name, request.CategoryId, request.SortOrder, request.ProductionMinutes, request.IsQueueIndependent, cancellationToken);

    return await AnsweredAsync(created, item => Results.Json(_mapper.Map<AdminItemView>(item), statusCode: StatusCodes.Status201Created));
  }

  public async Task<IResult> UpdateAsync(Guid itemId, SaveItemRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<Guid, CatalogItemAdministrationFailure> updated = await _service.UpdateAsync(itemId, request.Name, request.CategoryId, request.SortOrder, request.ProductionMinutes, request.IsQueueIndependent, cancellationToken);

    return await AnsweredAsync(updated, savedItemId => Results.Ok(new SavedItemView(savedItemId)));
  }

  public async Task<IResult> ActivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    Result<Guid, CatalogItemAdministrationFailure> switchedOn = await _service.ActivateAsync(itemId, cancellationToken);

    return await AnsweredAsync(switchedOn, savedItemId => Results.Ok(new SavedItemView(savedItemId)));
  }

  public async Task<IResult> DeactivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    Result<Guid, CatalogItemAdministrationFailure> switchedOff = await _service.DeactivateAsync(itemId, cancellationToken);

    return await AnsweredAsync(switchedOff, savedItemId => Results.Ok(new SavedItemView(savedItemId)));
  }

  private async Task<IResult> AnsweredAsync<TValue>(Result<TValue, CatalogItemAdministrationFailure> written, Func<TValue, IResult> buildResponse)
  {
    if (!written.IsSuccess)
      return RefusalFor(written.Failure);

    await _savedChangeAnnouncer.TellTheDevicesWithoutFailingTheSavedChangeAsync(_announcer.AnnounceAsync);

    return buildResponse(written.Value);
  }

  private IResult RefusalFor(CatalogItemAdministrationFailure failure)
  {
    return failure.Reason switch
           {
             CatalogItemAdministrationFailureReason.FestivalNotFound => Results.NotFound(),
             CatalogItemAdministrationFailureReason.ItemNotFound => Results.NotFound(),
             CatalogItemAdministrationFailureReason.NameMissing => _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "admin.itemNameMissing"),
             CatalogItemAdministrationFailureReason.NameTaken => _resultEnvelope.Problem(StatusCodes.Status409Conflict, "ItemNameTaken", "admin.itemNameTaken"),
             CatalogItemAdministrationFailureReason.ProductionMinutesOutOfRange => RefusedProductionMinutes(failure.OffendingProductionMinutes),
             CatalogItemAdministrationFailureReason.CategoryUnknown => _resultEnvelope.Problem(StatusCodes.Status422UnprocessableEntity, "UnprocessableEntity", "admin.itemCategoryUnknown"),
             CatalogItemAdministrationFailureReason.CategoryIsSwitchedOff => _resultEnvelope.Problem(StatusCodes.Status422UnprocessableEntity, "UnprocessableEntity", "admin.itemCategoryIsOff"),
             CatalogItemAdministrationFailureReason.ItemIsOnTheRunningFestivalsMenu => _resultEnvelope.Problem(StatusCodes.Status409Conflict, "ItemIsOnTheRunningFestivalsMenu", "admin.itemIsOnTheRunningFestivalsMenu"),
             _ => new UnreachableCase().Throw<IResult>(failure.Reason)
           };
  }

  private IResult RefusedProductionMinutes(double? offendingProductionMinutes)
  {
    _logger.LogWarning("An item was refused because its preparation time {ProductionMinutes} is not one the item form produces, which accepts 0 to 600 minutes with at most one decimal place, so this call did not come from that screen.", offendingProductionMinutes);

    return _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "catalog.productionMinutesOutOfRange");
  }
}
