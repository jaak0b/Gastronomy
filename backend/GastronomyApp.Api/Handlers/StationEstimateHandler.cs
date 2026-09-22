using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class StationEstimateHandler
{
  private readonly StationEstimateService _estimateService;
  private readonly IMapper _mapper;

  public StationEstimateHandler(StationEstimateService estimateService, IMapper mapper)
  {
    _estimateService = estimateService;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<StationEstimateListView>> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<Station> stations = await _estimateService.ReadStationsWithOpenWorkAsync(cancellationToken);

    return new StationEstimateListView(_mapper.Map<IReadOnlyList<StationEstimateView>>(stations));
  }
}
