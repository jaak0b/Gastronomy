using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.RateLimiting;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Endpoints;

public static class OrderEndpoints
{
  public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/orders")
                      .RequireAuthorization()
                      .RequireStaffDevice()
                      .RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

    group.MapPost(string.Empty,
                  async (PlaceOrderRequest request,
                         HttpContext httpContext,
                         CallerIdentity callerIdentity,
                         OrderPlacementHandler handler,
                         CancellationToken cancellationToken) =>
                  {
                    var caller = callerIdentity.ReadStaffDevice(httpContext.User)!;
                    return await handler.PlaceAsync(request, caller, cancellationToken);
                  });

    return routes;
  }
}

public sealed class OrderPlacementHandler
{
  private readonly OrderAcceptanceTransaction _acceptanceTransaction;
  private readonly GastronomyAppDbContext _dbContext;
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly ILogger<OrderPlacementHandler> _log;
  private readonly OrderReader _orderReader;
  private readonly ResultEnvelope _resultEnvelope;

  public OrderPlacementHandler(GastronomyAppDbContext dbContext,
                               OrderAcceptanceTransaction acceptanceTransaction,
                               OrderReader orderReader,
                               HubNotificationDispatcher dispatcher,
                               ResultEnvelope resultEnvelope,
                               ILogger<OrderPlacementHandler> log)
  {
    _dbContext = dbContext;
    _acceptanceTransaction = acceptanceTransaction;
    _orderReader = orderReader;
    _dispatcher = dispatcher;
    _resultEnvelope = resultEnvelope;
    _log = log;
  }

  public async Task<IResult> PlaceAsync(PlaceOrderRequest request,
                                        StaffDeviceCaller caller,
                                        CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    OrderAcceptanceRequest acceptanceRequest = new()
                                               {
                                                 ClientOrderId = request.ClientOrderId,
                                                 StaffMemberId = caller.StaffMemberId,
                                                 TableName = request.TableName ?? string.Empty,
                                                 Note = request.Note,
                                                 SettleOnSend = request.SettleOnSend,
                                                 Items =
                                                 [
                                                   .. (request.Items ?? []).Select(item => new OrderAcceptanceItemRequest
                                                                                           {
                                                                                             CatalogItemId = item.CatalogItemId,
                                                                                             UnitPriceCents = item.UnitPriceCents,
                                                                                             Note = item.Note,
                                                                                             StationId = item.StationId
                                                                                           })
                                                 ],
                                                 DeliveryModes =
                                                 [
                                                   .. (request.DeliveryModes ?? []).Select(mode => new StationDeliveryModeRequest
                                                                                                   {
                                                                                                     StationId = mode.StationId,
                                                                                                     DeliveryMode = mode.DeliveryMode
                                                                                                   })
                                                 ]
                                               };

    Result<OrderAcceptanceResult, OrderValidationFailure> acceptance =
      await _acceptanceTransaction.AcceptAsync(acceptanceRequest, cancellationToken);

    if (!acceptance.IsSuccess)
    {
      _log.LogWarning("The order {ClientOrderId} from staff member {StaffMemberId} was refused because {Reason}. "
                      + "The catalog item it names is {CatalogItemId}.",
                      request.ClientOrderId,
                      caller.StaffMemberId,
                      acceptance.Failure.Reason,
                      acceptance.Failure.OffendingCatalogItemId);

      return _resultEnvelope.ToResult(_resultEnvelope.Describe(acceptance.Failure));
    }

    var stored = (await _orderReader.LoadAsync(_dbContext, acceptance.Value.Order.Id, cancellationToken))!;
    var view = _orderReader.Describe(stored);

    await TellEveryStationThatGotASliceAsync(view, cancellationToken);

    return Results.Json(view,
                        statusCode: acceptance.Value.WasAlreadyAccepted
                                      ? StatusCodes.Status200OK
                                      : StatusCodes.Status201Created);
  }

  private async Task TellEveryStationThatGotASliceAsync(PlacedOrderView view, CancellationToken cancellationToken)
  {
    foreach (var stationId in view.StationOrders.Select(stationOrder => stationOrder.StationId).Distinct())
    {
      await _dispatcher.PushStationOrdersChangedAsync(stationId, cancellationToken);
    }
  }
}

