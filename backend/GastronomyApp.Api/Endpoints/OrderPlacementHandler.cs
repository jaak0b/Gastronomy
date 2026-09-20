using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Endpoints;

public sealed class OrderPlacementHandler
{
  private readonly OrderAcceptanceService _acceptanceService;
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly ILogger<OrderPlacementHandler> _log;
  private readonly PlacedOrderReader _placedOrderReader;
  private readonly ResultEnvelope _resultEnvelope;

  public OrderPlacementHandler(OrderAcceptanceService acceptanceService, PlacedOrderReader placedOrderReader, HubNotificationDispatcher dispatcher, ResultEnvelope resultEnvelope, ILogger<OrderPlacementHandler> log)
  {
    _acceptanceService = acceptanceService;
    _placedOrderReader = placedOrderReader;
    _dispatcher = dispatcher;
    _resultEnvelope = resultEnvelope;
    _log = log;
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
    var view = BuildPlacedOrderView(report);

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
             Items = (request.Items ?? []).Select(BuildAcceptanceItem).ToList(),
             DeliveryModes = (request.DeliveryModes ?? []).Select(mode => new StationDeliveryModeRequest
                                                                          {
                                                                            StationId = mode.StationId,
                                                                            DeliveryMode = mode.DeliveryMode
                                                                          })
                                                          .ToList()
           };
  }

  private OrderAcceptanceItemRequest BuildAcceptanceItem(OrderItemRequest item)
  {
    return new()
           {
             CatalogItemId = item.CatalogItemId,
             UnitPriceCents = item.UnitPriceCents,
             Note = item.Note,
             StationId = item.StationId,
             Settlement = BuildSettlementTerms(item)
           };
  }

  private OrderSettlementLineTerms? BuildSettlementTerms(OrderItemRequest item)
  {
    if (item.Settlement is null)
      return null;

    return new()
           {
             PaidPriceCents = item.Settlement.PaidPriceCents,
             PaymentNotice = item.Settlement.PaymentNotice
           };
  }

  private PlacedOrderView BuildPlacedOrderView(PlacedOrderReport report)
  {
    return new(report.Order.OrderId, report.Order.GlobalOrderNumber, report.Status, report.TotalCents, report.Order.CreatedAtUtc, report.Order.StationOrders.Select(BuildStationOrderView).ToList());
  }

  private StationOrderView BuildStationOrderView(PlacedStationOrder stationOrder)
  {
    return new(stationOrder.StationOrderId, stationOrder.StationId, stationOrder.StationName, stationOrder.StationOrderNumber, stationOrder.DeliveryMode, stationOrder.Items.Select(item => item.OrderItemId).ToList());
  }

  private void WarnAboutTheRefusal(PlaceOrderRequest request, StaffDeviceCaller caller, OrderValidationFailure failure)
  {
    _log.LogWarning("The order {ClientOrderId} from staff member {StaffMemberId} was refused because {Reason}. " + "The catalog item it names is {CatalogItemId}.", request.ClientOrderId, caller.StaffMemberId, failure.Reason, failure.OffendingCatalogItemId);

    if (failure.SettlementFailureReason is { } settlementFailureReason)
    {
      _log.LogWarning("The settlement of the order {ClientOrderId} from staff member {StaffMemberId} was refused because {SettlementFailureReason}.", request.ClientOrderId, caller.StaffMemberId, settlementFailureReason);
    }
  }

  private async Task TellEveryStationWithAStationOrderAsync(PlacedOrderView view, CancellationToken cancellationToken)
  {
    foreach (var stationId in view.StationOrders.Select(stationOrder => stationOrder.StationId).Distinct())
      await _dispatcher.PushStationOrdersChangedAsync(stationId, cancellationToken);
  }
}
