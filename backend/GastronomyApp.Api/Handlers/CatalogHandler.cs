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
    var festival = await _catalogService.ReadRunningFestivalCatalogAsync(cancellationToken);

    if (festival is null)
      return Results.Ok(new CatalogView(null, [], [], []));

    return Results.Ok(_mapper.Map<CatalogView>(festival));
  }
}
