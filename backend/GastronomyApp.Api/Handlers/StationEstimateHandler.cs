using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

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

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<Station> stations = await _estimateService.ReadStationsWithOpenWorkAsync(cancellationToken);

    return Results.Ok(new StationEstimateListView(_mapper.Map<IReadOnlyList<StationEstimateView>>(stations)));
  }
}
