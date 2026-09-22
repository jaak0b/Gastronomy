using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminFestivalMenuHandler
{
  private readonly FestivalMenuService _service;

  public AdminFestivalMenuHandler(FestivalMenuService service)
  {
    _service = service;
  }

  public async Task<ApiAnswer<SavedItemView>> PutOnTheMenuAsync(Guid festivalId, Guid itemId, SaveFestivalItemRequest request, CancellationToken cancellationToken)
  {
    return await _service.PutOnTheMenuAsync(festivalId, itemId, request.PriceCents, request.StationIds, cancellationToken).Then(menuRow => new SavedItemView(menuRow.CatalogItemId));
  }

  public async Task<NoContentAnswer> TakeOffTheMenuAsync(Guid festivalId, Guid itemId, CancellationToken cancellationToken)
  {
    return await _service.TakeOffTheMenuAsync(festivalId, itemId, cancellationToken).Then(menuRow => Result.Success);
  }

  public async Task<ApiAnswer<SavedItemView>> SetAvailabilityAsync(Guid festivalId, Guid itemId, SetAvailabilityRequest request, CancellationToken cancellationToken)
  {
    return await _service.SetAvailabilityAsync(festivalId, itemId, request.IsAvailable, cancellationToken).Then(menuRow => new SavedItemView(menuRow.CatalogItemId));
  }
}
