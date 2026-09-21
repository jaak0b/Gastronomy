using ErrorOr;
using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminItemHandler
{
  private readonly CatalogChangeAnnouncer _announcer;
  private readonly IMapper _mapper;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly SavedChangeAnnouncer _savedChangeAnnouncer;
  private readonly CatalogItemAdministrationService _service;

  public AdminItemHandler(CatalogItemAdministrationService service, CatalogChangeAnnouncer announcer, SavedChangeAnnouncer savedChangeAnnouncer, ResultEnvelope resultEnvelope, IMapper mapper)
  {
    _service = service;
    _announcer = announcer;
    _savedChangeAnnouncer = savedChangeAnnouncer;
    _resultEnvelope = resultEnvelope;
    _mapper = mapper;
  }

  public async Task<IResult> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    return await _service.ListAsync(festivalId, cancellationToken)
                         .Match(items => Results.Ok(new AdminItemListView(_mapper.Map<IReadOnlyList<AdminItemView>>(items))), _resultEnvelope.Refuse);
  }

  public async Task<IResult> CreateAsync(SaveItemRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return await _service.CreateAsync(request.Name, request.CategoryId, request.SortOrder, request.ProductionMinutes, request.IsQueueIndependent, cancellationToken)
                         .ThenDoAsync(item => TellTheDevicesAsync())
                         .Match(item => Results.Json(_mapper.Map<AdminItemView>(item), statusCode: StatusCodes.Status201Created), _resultEnvelope.Refuse);
  }

  public async Task<IResult> UpdateAsync(Guid itemId, SaveItemRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return await _service.UpdateAsync(itemId, request.Name, request.CategoryId, request.SortOrder, request.ProductionMinutes, request.IsQueueIndependent, cancellationToken)
                         .ThenDoAsync(item => TellTheDevicesAsync())
                         .Match(item => Results.Ok(new SavedItemView(item.Id)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> ActivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    return await _service.ActivateAsync(itemId, cancellationToken)
                         .ThenDoAsync(item => TellTheDevicesAsync())
                         .Match(item => Results.Ok(new SavedItemView(item.Id)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> DeactivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    return await _service.DeactivateAsync(itemId, cancellationToken)
                         .ThenDoAsync(item => TellTheDevicesAsync())
                         .Match(item => Results.Ok(new SavedItemView(item.Id)), _resultEnvelope.Refuse);
  }

  private Task TellTheDevicesAsync()
  {
    return _savedChangeAnnouncer.TellTheDevicesWithoutFailingTheSavedChangeAsync(_announcer.AnnounceAsync);
  }
}
