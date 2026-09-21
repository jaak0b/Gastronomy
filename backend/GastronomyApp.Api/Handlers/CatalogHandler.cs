using ErrorOr;
using GastronomyApp.Contracts.Catalog;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class CatalogHandler
{
  private readonly CatalogService _catalogService;
  private readonly IMapper _mapper;

  public CatalogHandler(CatalogService catalogService, IMapper mapper)
  {
    _catalogService = catalogService;
    _mapper = mapper;
  }

  public async Task<IResult> ReadAsync(CancellationToken cancellationToken)
  {
    return await _catalogService.ReadRunningFestivalCatalogAsync(cancellationToken).Match(festival => Results.Ok(_mapper.Map<CatalogView>(festival)), noFestivalIsRunning => Results.Ok(CatalogView.Empty));
  }
}
