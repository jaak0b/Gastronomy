using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Endpoints;

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
    var catalog = await _catalogService.ReadRunningFestivalCatalogAsync(cancellationToken);

    if (catalog is null)
      return Results.Ok(new CatalogView(null, [], [], []));

    return Results.Ok(_mapper.Map<CatalogView>(catalog));
  }
}
