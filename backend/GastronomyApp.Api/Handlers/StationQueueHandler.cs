using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.Responders;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using GastronomyApp.Api.Values;

namespace GastronomyApp.Api.Handlers;

public sealed class StationQueueHandler
{
  private readonly IMapper _mapper;
  private readonly StationQueueService _queueService;
  private readonly StationQueueRefusalResponder _refusalResponder;

  public StationQueueHandler(StationQueueService queueService, IMapper mapper, StationQueueRefusalResponder refusalResponder)
  {
    _queueService = queueService;
    _mapper = mapper;
    _refusalResponder = refusalResponder;
  }

  public async Task<IResult> ListQueueAsync(StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationQueue, StationQueueFailure> queue = await _queueService.ReadQueueAsync(caller.StationId, cancellationToken);

    if (!queue.IsSuccess)
      return _refusalResponder.Respond(queue.Failure);

    return Results.Ok(_mapper.Map<StationQueueView>(queue.Value));
  }

  public async Task<IResult> ListFulfilledAsync(StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    Result<IReadOnlyList<QueuedStationOrder>, StationQueueFailure> stationOrders = await _queueService.ReadFulfilledAsync(caller.StationId, cancellationToken);

    if (!stationOrders.IsSuccess)
      return _refusalResponder.Respond(stationOrders.Failure);

    return Results.Ok(new StationFulfilledView(_mapper.Map<IReadOnlyList<StationOrderQueueView>>(stationOrders.Value)));
  }
}
