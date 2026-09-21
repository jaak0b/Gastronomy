using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Handlers;

public sealed class OrderPlacementHandler
{
  private readonly OrderAcceptanceService _acceptanceService;
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly ILogger<OrderPlacementHandler> _log;
  private readonly IMapper _mapper;
  private readonly IOrderRepository _orderRepository;
  private readonly ResultEnvelope _resultEnvelope;

  public OrderPlacementHandler(OrderAcceptanceService acceptanceService, IOrderRepository orderRepository, HubNotificationDispatcher dispatcher, ResultEnvelope resultEnvelope, ILogger<OrderPlacementHandler> log, IMapper mapper)
  {
    _acceptanceService = acceptanceService;
    _orderRepository = orderRepository;
    _dispatcher = dispatcher;
    _resultEnvelope = resultEnvelope;
    _log = log;
    _mapper = mapper;
  }

  public async Task<IResult> PlaceAsync(PlaceOrderRequest request, StaffDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    Result<Order, OrderValidationFailure> acceptance = await _acceptanceService.AcceptAsync(request, caller.StaffMemberId, cancellationToken);

    if (!acceptance.IsSuccess)
    {
      WarnAboutTheRefusal(request, caller, acceptance.Failure);

      return _resultEnvelope.ToResult(_resultEnvelope.BuildProblemDescription(acceptance.Failure));
    }

    var storedOrder = (await _orderRepository.FindWithStationOrdersAsync(acceptance.Value.Id, cancellationToken))!;
    var view = _mapper.Map<PlacedOrderView>(storedOrder);

    await TellEveryStationWithAStationOrderAsync(view, cancellationToken);

    return Results.Json(view, statusCode: StatusCodes.Status201Created);
  }

  private void WarnAboutTheRefusal(PlaceOrderRequest request, StaffDeviceCaller caller, OrderValidationFailure failure)
  {
    _log.LogWarning("The order {ClientOrderId} from staff member {StaffMemberId} was refused because {Reason}. The catalog item it names is {CatalogItemId}.", request.ClientOrderId, caller.StaffMemberId, failure.Reason, failure.OffendingCatalogItemId);

    if (failure.SettlementFailureReason is { } settlementFailureReason)
      _log.LogWarning("The settlement of the order {ClientOrderId} from staff member {StaffMemberId} was refused because {SettlementFailureReason}.", request.ClientOrderId, caller.StaffMemberId, settlementFailureReason);
  }

  private async Task TellEveryStationWithAStationOrderAsync(PlacedOrderView view, CancellationToken cancellationToken)
  {
    foreach (var stationId in view.StationOrders.Select(stationOrder => stationOrder.StationId).Distinct())
      await _dispatcher.PushStationOrdersChangedAsync(stationId, cancellationToken);
  }
}
