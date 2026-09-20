using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class StationEstimateHandler
{
  private readonly StationEstimateService _estimateService;

  public StationEstimateHandler(StationEstimateService estimateService)
  {
    _estimateService = estimateService;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<StationEstimate> estimates = await _estimateService.ReadAsync(cancellationToken);

    return Results.Ok(new StationEstimateListView(estimates.Select(estimate => new StationEstimateView(estimate.StationId, estimate.QueuedMinutes)).ToList()));
  }
}
