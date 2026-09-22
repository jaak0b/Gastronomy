using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Catalog;
using GastronomyApp.Core.Services;
using MapsterMapper;

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

  public async Task<ApiAnswer<CatalogView>> ReadAsync(CancellationToken cancellationToken)
  {
    ErrorOr<Core.Entities.Festival> runningFestival = await _catalogService.ReadRunningFestivalCatalogAsync(cancellationToken);

    return runningFestival.Match<ErrorOr<CatalogView>>(festival => _mapper.Map<CatalogView>(festival), noFestivalIsRunning => CatalogView.Empty);
  }
}
