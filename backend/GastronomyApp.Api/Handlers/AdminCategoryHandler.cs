using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminCategoryHandler
{
  private readonly IMapper _mapper;
  private readonly CatalogCategoryAdministrationService _service;

  public AdminCategoryHandler(CatalogCategoryAdministrationService service, IMapper mapper)
  {
    _service = service;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<AdminCategoryListView>> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<CatalogCategory> categories = await _service.ListAsync(cancellationToken);

    return _mapper.Map<IReadOnlyList<CatalogCategory>, AdminCategoryListView>(categories);
  }

  public async Task<CreatedAnswer<AdminCategoryView>> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken)
  {
    return await _service.CreateAsync(request.Name, request.ColourHex, cancellationToken).Then(_mapper.Map<AdminCategoryView>);
  }

  public async Task<ApiAnswer<AdminCategoryView>> UpdateAsync(Guid categoryId, SaveCategoryRequest request, CancellationToken cancellationToken)
  {
    return await _service.UpdateAsync(categoryId, request.Name, request.ColourHex, cancellationToken).Then(_mapper.Map<AdminCategoryView>);
  }

  public async Task<ApiAnswer<AdminCategoryListView>> MoveAsync(Guid categoryId, MoveCategoryRequest request, CancellationToken cancellationToken)
  {
    return await _service.MoveAsync(categoryId, request.Direction, cancellationToken).Then(_mapper.Map<IReadOnlyList<CatalogCategory>, AdminCategoryListView>);
  }

  public async Task<ApiAnswer<AdminCategoryView>> ActivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    return await _service.ActivateAsync(categoryId, cancellationToken).Then(_mapper.Map<AdminCategoryView>);
  }

  public async Task<ApiAnswer<AdminCategoryView>> DeactivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    return await _service.DeactivateAsync(categoryId, cancellationToken).Then(_mapper.Map<AdminCategoryView>);
  }
}
