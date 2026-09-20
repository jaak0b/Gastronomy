using GastronomyApp.Api.Auth.Callers;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Responders;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class StationFulfillmentHandler
{
  private readonly StationQueueChangeService _changeService;
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly IMapper _mapper;
  private readonly StationQueueRefusalResponder _refusalResponder;

  public StationFulfillmentHandler(StationQueueChangeService changeService, HubNotificationDispatcher dispatcher, IMapper mapper, StationQueueRefusalResponder refusalResponder)
  {
    _changeService = changeService;
    _dispatcher = dispatcher;
    _mapper = mapper;
    _refusalResponder = refusalResponder;
  }

  public async Task<IResult> FulfillAsync(StationItemSelectionRequest request, StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationQueueChange, StationQueueFailure> change = await _changeService.FulfillAsync(request.OrderItemIds ?? [], caller.StationId, cancellationToken);

    return await RespondAsync(change, caller.StationId, cancellationToken);
  }

  public async Task<IResult> UnfulfillAsync(StationItemSelectionRequest request, StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationQueueChange, StationQueueFailure> change = await _changeService.UnfulfillAsync(request.OrderItemIds ?? [], caller.StationId, cancellationToken);

    return await RespondAsync(change, caller.StationId, cancellationToken);
  }

  public async Task<IResult> HideAsync(Guid stationOrderId, StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationQueueChange, StationQueueFailure> change = await _changeService.HideFromAsItComesQueueAsync(stationOrderId, caller.StationId, cancellationToken);

    return await RespondAsync(change, caller.StationId, cancellationToken);
  }

  private async Task<IResult> RespondAsync(Result<StationQueueChange, StationQueueFailure> change, Guid stationId, CancellationToken cancellationToken)
  {
    if (!change.IsSuccess)
      return _refusalResponder.Respond(change.Failure);

    await _dispatcher.PushStationOrdersChangedAsync(stationId, cancellationToken);

    foreach (var statusChange in change.Value.OrderStatusChanges)
      await _dispatcher.PushOrderStatusChangedAsync(statusChange.OrderId, statusChange.Status, cancellationToken);

    return Results.Ok(_mapper.Map<StationQueueView>(change.Value.Queue));
  }
}
