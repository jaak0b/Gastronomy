using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Core.Requests;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using GastronomyApp.Api.Values;

namespace GastronomyApp.Api.Handlers;

public sealed class OrderPlacementHandler
{
  private readonly OrderAcceptanceService _acceptanceService;
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly ILogger<OrderPlacementHandler> _log;
  private readonly IMapper _mapper;
  private readonly PlacedOrderReader _placedOrderReader;
  private readonly ResultEnvelope _resultEnvelope;

  public OrderPlacementHandler(OrderAcceptanceService acceptanceService, PlacedOrderReader placedOrderReader, HubNotificationDispatcher dispatcher, ResultEnvelope resultEnvelope, ILogger<OrderPlacementHandler> log, IMapper mapper)
  {
    _acceptanceService = acceptanceService;
    _placedOrderReader = placedOrderReader;
    _dispatcher = dispatcher;
    _resultEnvelope = resultEnvelope;
    _log = log;
    _mapper = mapper;
  }

  public async Task<IResult> PlaceAsync(PlaceOrderRequest request, StaffDeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    Result<OrderAcceptanceResult, OrderValidationFailure> acceptance = await _acceptanceService.AcceptAsync(BuildAcceptanceRequest(request, caller), cancellationToken);

    if (!acceptance.IsSuccess)
    {
      WarnAboutTheRefusal(request, caller, acceptance.Failure);

      return _resultEnvelope.ToResult(_resultEnvelope.BuildProblemDescription(acceptance.Failure));
    }

    var report = (await _placedOrderReader.FindAsync(acceptance.Value.Order.Id, cancellationToken))!;
    var view = _mapper.Map<PlacedOrderView>(report);

    await TellEveryStationWithAStationOrderAsync(view, cancellationToken);

    if (acceptance.Value.WasAlreadyAccepted)
      return Results.Json(view, statusCode: StatusCodes.Status200OK);

    return Results.Json(view, statusCode: StatusCodes.Status201Created);
  }

  private OrderAcceptanceRequest BuildAcceptanceRequest(PlaceOrderRequest request, StaffDeviceCaller caller)
  {
    return new()
           {
             ClientOrderId = request.ClientOrderId,
             StaffMemberId = caller.StaffMemberId,
             TableName = request.TableName ?? string.Empty,
             Note = request.Note,
             Items = _mapper.Map<IReadOnlyList<OrderAcceptanceItemRequest>>(request.Items ?? []),
             DeliveryModes = _mapper.Map<IReadOnlyList<StationDeliveryModeRequest>>(request.DeliveryModes ?? [])
           };
  }

  private void WarnAboutTheRefusal(PlaceOrderRequest request, StaffDeviceCaller caller, OrderValidationFailure failure)
  {
    _log.LogWarning("The order {ClientOrderId} from staff member {StaffMemberId} was refused because {Reason}. " + "The catalog item it names is {CatalogItemId}.", request.ClientOrderId, caller.StaffMemberId, failure.Reason, failure.OffendingCatalogItemId);

    if (failure.SettlementFailureReason is { } settlementFailureReason)
      _log.LogWarning("The settlement of the order {ClientOrderId} from staff member {StaffMemberId} was refused because {SettlementFailureReason}.", request.ClientOrderId, caller.StaffMemberId, settlementFailureReason);
  }

  private async Task TellEveryStationWithAStationOrderAsync(PlacedOrderView view, CancellationToken cancellationToken)
  {
    foreach (var stationId in view.StationOrders.Select(stationOrder => stationOrder.StationId).Distinct())
      await _dispatcher.PushStationOrdersChangedAsync(stationId, cancellationToken);
  }
}
