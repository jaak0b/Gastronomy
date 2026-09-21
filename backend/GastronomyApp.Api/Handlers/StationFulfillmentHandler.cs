using ErrorOr;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Events;
using GastronomyApp.Contracts.Stations;
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
  private readonly ResultEnvelope _resultEnvelope;

  public StationFulfillmentHandler(StationQueueChangeService changeService, HubNotificationDispatcher dispatcher, IMapper mapper, ResultEnvelope resultEnvelope)
  {
    _changeService = changeService;
    _dispatcher = dispatcher;
    _mapper = mapper;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> FulfillAsync(StationItemSelectionRequest request, StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    return await RespondAsync(_changeService.FulfillAsync(request.OrderItemIds ?? [], caller.StationId, cancellationToken), caller.StationId, cancellationToken);
  }

  public async Task<IResult> UnfulfillAsync(StationItemSelectionRequest request, StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    return await RespondAsync(_changeService.UnfulfillAsync(request.OrderItemIds ?? [], caller.StationId, cancellationToken), caller.StationId, cancellationToken);
  }

  public async Task<IResult> HideAsync(Guid stationOrderId, StationDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    return await RespondAsync(_changeService.HideFromAsItComesQueueAsync(stationOrderId, caller.StationId, cancellationToken), caller.StationId, cancellationToken);
  }

  private Task<IResult> RespondAsync(Task<ErrorOr<StationQueueChange>> change, Guid stationId, CancellationToken cancellationToken)
  {
    return change.ThenDoAsync(changed => TellEveryoneAboutAsync(changed, stationId, cancellationToken))
                 .Match(changed => Results.Ok(_mapper.Map<StationQueueView>(changed.Station)), _resultEnvelope.Refuse);
  }

  private async Task TellEveryoneAboutAsync(StationQueueChange change, Guid stationId, CancellationToken cancellationToken)
  {
    await _dispatcher.PushStationOrdersChangedAsync(stationId, cancellationToken);

    foreach (var changedOrder in change.ChangedOrders)
      await _dispatcher.PushOrderStatusChangedAsync(_mapper.Map<OrderStatusChangedEvent>(changedOrder), cancellationToken);
  }
}
