using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.Hub;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Endpoints;

public sealed class StationFulfillmentHandler
{
  private readonly StationQueueChangeService _changeService;
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly StationQueueRefusalResponder _refusalResponder;
  private readonly StationQueueViewBuilder _viewBuilder;

  public StationFulfillmentHandler(StationQueueChangeService changeService,
                                   HubNotificationDispatcher dispatcher,
                                   StationQueueViewBuilder viewBuilder,
                                   StationQueueRefusalResponder refusalResponder)
  {
    _changeService = changeService;
    _dispatcher = dispatcher;
    _viewBuilder = viewBuilder;
    _refusalResponder = refusalResponder;
  }

  public async Task<IResult> FulfillAsync(StationItemSelectionRequest request,
                                          StationDeviceCaller caller,
                                          CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationQueueChange, StationQueueFailure> change =
      await _changeService.FulfillAsync(request.OrderItemIds ?? [], caller.StationId, cancellationToken);

    return await RespondAsync(change, caller.StationId, cancellationToken);
  }

  public async Task<IResult> UnfulfillAsync(StationItemSelectionRequest request,
                                            StationDeviceCaller caller,
                                            CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationQueueChange, StationQueueFailure> change =
      await _changeService.UnfulfillAsync(request.OrderItemIds ?? [], caller.StationId, cancellationToken);

    return await RespondAsync(change, caller.StationId, cancellationToken);
  }

  public async Task<IResult> HideAsync(Guid stationOrderId,
                                       StationDeviceCaller caller,
                                       CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    Result<StationQueueChange, StationQueueFailure> change =
      await _changeService.HideFromAsItComesQueueAsync(stationOrderId, caller.StationId, cancellationToken);

    return await RespondAsync(change, caller.StationId, cancellationToken);
  }

  private async Task<IResult> RespondAsync(Result<StationQueueChange, StationQueueFailure> change,
                                           Guid stationId,
                                           CancellationToken cancellationToken)
  {
    if (!change.IsSuccess)
    {
      return _refusalResponder.Respond(change.Failure);
    }

    await _dispatcher.PushStationOrdersChangedAsync(stationId, cancellationToken);

    foreach (var statusChange in change.Value.OrderStatusChanges)
    {
      await _dispatcher.PushOrderStatusChangedAsync(statusChange.OrderId, statusChange.Status, cancellationToken);
    }

    return Results.Ok(_viewBuilder.Build(change.Value.Queue));
  }
}
