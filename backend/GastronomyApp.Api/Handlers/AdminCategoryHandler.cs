using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminCategoryHandler
{
  private readonly IMapper _mapper;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly CatalogCategoryAdministrationService _service;

  public AdminCategoryHandler(CatalogCategoryAdministrationService service, ResultEnvelope resultEnvelope, IMapper mapper)
  {
    _service = service;
    _resultEnvelope = resultEnvelope;
    _mapper = mapper;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<CatalogCategory> categories = await _service.ListAsync(cancellationToken);

    return Results.Ok(_mapper.Map<IReadOnlyList<CatalogCategory>, AdminCategoryListView>(categories));
  }

  public async Task<IResult> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return await _service.CreateAsync(request.Name, request.ColourHex, cancellationToken).Match(category => Results.Json(_mapper.Map<AdminCategoryView>(category), statusCode: StatusCodes.Status201Created), _resultEnvelope.Refuse);
  }

  public async Task<IResult> UpdateAsync(Guid categoryId, SaveCategoryRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return await _service.UpdateAsync(categoryId, request.Name, request.ColourHex, cancellationToken).Match(category => Results.Ok(_mapper.Map<AdminCategoryView>(category)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> MoveAsync(Guid categoryId, MoveCategoryRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return await _service.MoveAsync(categoryId, request.Direction, cancellationToken).Match(categories => Results.Ok(_mapper.Map<IReadOnlyList<CatalogCategory>, AdminCategoryListView>(categories)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> ActivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    return await _service.ActivateAsync(categoryId, cancellationToken).Match(category => Results.Ok(_mapper.Map<AdminCategoryView>(category)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> DeactivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    return await _service.DeactivateAsync(categoryId, cancellationToken).Match(category => Results.Ok(_mapper.Map<AdminCategoryView>(category)), _resultEnvelope.Refuse);
  }
}
