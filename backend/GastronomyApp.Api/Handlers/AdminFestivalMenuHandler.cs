using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminFestivalMenuHandler
{
  private readonly ResultEnvelope _resultEnvelope;
  private readonly FestivalMenuService _service;

  public AdminFestivalMenuHandler(FestivalMenuService service, ResultEnvelope resultEnvelope)
  {
    _service = service;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> PutOnTheMenuAsync(Guid festivalId, Guid itemId, SaveFestivalItemRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return await _service.PutOnTheMenuAsync(festivalId, itemId, request.PriceCents, request.StationIds, cancellationToken).Match(menuRow => Results.Ok(new SavedItemView(menuRow.CatalogItemId)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> TakeOffTheMenuAsync(Guid festivalId, Guid itemId, CancellationToken cancellationToken)
  {
    return await _service.TakeOffTheMenuAsync(festivalId, itemId, cancellationToken).Match(menuRow => Results.NoContent(), _resultEnvelope.Refuse);
  }

  public async Task<IResult> SetAvailabilityAsync(Guid festivalId, Guid itemId, SetAvailabilityRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return await _service.SetAvailabilityAsync(festivalId, itemId, request.IsAvailable, cancellationToken).Match(menuRow => Results.Ok(new SavedItemView(menuRow.CatalogItemId)), _resultEnvelope.Refuse);
  }
}
