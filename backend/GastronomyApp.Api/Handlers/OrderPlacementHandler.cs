using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class OrderPlacementHandler
{
  private readonly OrderAcceptanceService _acceptanceService;
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly IMapper _mapper;
  private readonly IOrderRepository _orderRepository;
  private readonly ResultEnvelope _resultEnvelope;

  public OrderPlacementHandler(OrderAcceptanceService acceptanceService, IOrderRepository orderRepository, HubNotificationDispatcher dispatcher, ResultEnvelope resultEnvelope, IMapper mapper)
  {
    _acceptanceService = acceptanceService;
    _orderRepository = orderRepository;
    _dispatcher = dispatcher;
    _resultEnvelope = resultEnvelope;
    _mapper = mapper;
  }

  public async Task<IResult> PlaceAsync(PlaceOrderRequest request, StaffDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    return await _acceptanceService.AcceptAsync(request, caller.StaffMemberId, cancellationToken)
                                   .MatchAsync(order => AnnounceAndAnswerAsync(order, cancellationToken), refusedLines => Task.FromResult(_resultEnvelope.Refuse(refusedLines)));
  }

  private async Task<IResult> AnnounceAndAnswerAsync(Order acceptedOrder, CancellationToken cancellationToken)
  {
    var storedOrder = (await _orderRepository.FindWithStationOrdersAsync(acceptedOrder.Id, cancellationToken))!;
    var view = _mapper.Map<PlacedOrderView>(storedOrder);

    await TellEveryStationWithAStationOrderAsync(view, cancellationToken);

    return Results.Json(view, statusCode: StatusCodes.Status201Created);
  }

  private async Task TellEveryStationWithAStationOrderAsync(PlacedOrderView view, CancellationToken cancellationToken)
  {
    foreach (var stationId in view.StationOrders.Select(stationOrder => stationOrder.StationId).Distinct())
      await _dispatcher.PushStationOrdersChangedAsync(stationId, cancellationToken);
  }

}
