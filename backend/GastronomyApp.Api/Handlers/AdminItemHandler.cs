using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminItemHandler
{
  private readonly IMapper _mapper;
  private readonly CatalogItemAdministrationService _service;

  public AdminItemHandler(CatalogItemAdministrationService service, IMapper mapper)
  {
    _service = service;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<AdminItemListView>> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    return await _service.ListAsync(festivalId, cancellationToken).Then(items => new AdminItemListView(_mapper.Map<IReadOnlyList<AdminItemView>>(items)));
  }

  public async Task<CreatedAnswer<AdminItemView>> CreateAsync(SaveItemRequest request, CancellationToken cancellationToken)
  {
    return await _service.CreateAsync(request.Name, request.CategoryId, request.SortOrder, request.ProductionMinutes, request.IsQueueIndependent, cancellationToken).Then(_mapper.Map<AdminItemView>);
  }

  public async Task<ApiAnswer<SavedItemView>> UpdateAsync(Guid itemId, SaveItemRequest request, CancellationToken cancellationToken)
  {
    return await _service.UpdateAsync(itemId, request.Name, request.CategoryId, request.SortOrder, request.ProductionMinutes, request.IsQueueIndependent, cancellationToken).Then(item => new SavedItemView(item.Id));
  }

  public async Task<ApiAnswer<SavedItemView>> ActivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    return await _service.ActivateAsync(itemId, cancellationToken).Then(item => new SavedItemView(item.Id));
  }

  public async Task<ApiAnswer<SavedItemView>> DeactivateAsync(Guid itemId, CancellationToken cancellationToken)
  {
    return await _service.DeactivateAsync(itemId, cancellationToken).Then(item => new SavedItemView(item.Id));
  }
}
