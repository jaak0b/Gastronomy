using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Endpoints;

public sealed class StationQueueHandler
{
  private readonly StationQueueService _queueService;
  private readonly StationQueueRefusalResponder _refusalResponder;
  private readonly StationQueueViewBuilder _viewBuilder;

  public StationQueueHandler(StationQueueService queueService, StationQueueViewBuilder viewBuilder, StationQueueRefusalResponder refusalResponder)
  {
    _queueService = queueService;
    _viewBuilder = viewBuilder;
    _refusalResponder = refusalResponder;
  }

  public async Task<IResult> ListQueueAsync(StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationQueue, StationQueueFailure> queue = await _queueService.ReadQueueAsync(caller.StationId, cancellationToken);

    if (!queue.IsSuccess)
      return _refusalResponder.Respond(queue.Failure);

    return Results.Ok(_viewBuilder.Build(queue.Value));
  }

  public async Task<IResult> ListFulfilledAsync(StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    Result<IReadOnlyList<QueuedStationOrder>, StationQueueFailure> stationOrders = await _queueService.ReadFulfilledAsync(caller.StationId, cancellationToken);

    if (!stationOrders.IsSuccess)
      return _refusalResponder.Respond(stationOrders.Failure);

    return Results.Ok(new StationFulfilledView(_viewBuilder.BuildStationOrders(stationOrders.Value)));
  }
}
