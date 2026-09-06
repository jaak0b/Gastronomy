using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.RateLimiting;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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
  private readonly OrderReader _orderReader;
  private readonly ResultEnvelope _resultEnvelope;

  public OrderPlacementHandler(GastronomyAppDbContext dbContext,
                               OrderAcceptanceTransaction acceptanceTransaction,
                               OrderReader orderReader,
                               HubNotificationDispatcher dispatcher,
                               ResultEnvelope resultEnvelope)
  {
    _dbContext = dbContext;
    _acceptanceTransaction = acceptanceTransaction;
    _orderReader = orderReader;
    _dispatcher = dispatcher;
    _resultEnvelope = resultEnvelope;
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
      return _resultEnvelope.ToResult(_resultEnvelope.Describe(acceptance.Failure));
    }

    var orderId = acceptance.Value.Order.Id;

    if (acceptance.Value.WasAlreadyAccepted)
    {
      var existing = await _orderReader.LoadAsync(_dbContext, orderId, cancellationToken);

      if (existing is null || !new SubmissionComparison().Matches(request, existing))
      {
        return _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                      "SubmissionIdReused",
                                      "order.submissionIdReused");
      }

      var repeated = _orderReader.Describe(existing);
      await TellEveryStationThatGotASliceAsync(repeated, cancellationToken);

      return Results.Json(repeated, statusCode: StatusCodes.Status200OK);
    }

    var placed = (await _orderReader.LoadAsync(_dbContext, orderId, cancellationToken))!;
    var view = _orderReader.Describe(placed);

    await _dispatcher.PushOrderAcceptedAsync(new(view.OrderId,
                                                view.GlobalOrderNumber,
                                                placed.Order.TableName,
                                                view.TotalCents,
                                                view.StationOrders),
                                            cancellationToken);
    await TellEveryStationThatGotASliceAsync(view, cancellationToken);

    return Results.Json(view, statusCode: StatusCodes.Status201Created);
  }

  private async Task TellEveryStationThatGotASliceAsync(PlacedOrderView view, CancellationToken cancellationToken)
  {
    foreach (var stationId in view.StationOrders.Select(stationOrder => stationOrder.StationId).Distinct())
    {
      await _dispatcher.PushStationOrdersChangedAsync(stationId, cancellationToken);
    }
  }
}

public sealed class SubmissionComparison
{
  public bool Matches(PlaceOrderRequest request, LoadedOrder existing)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(existing);

    if (!string.Equals(request.TableName ?? string.Empty, existing.Order.TableName, StringComparison.Ordinal))
    {
      return false;
    }

    if (!string.Equals(request.Note ?? string.Empty, existing.Order.Note ?? string.Empty, StringComparison.Ordinal))
    {
      return false;
    }

    List<OrderItemRequest> requestItems = [.. request.Items ?? []];

    if (requestItems.Count != existing.Items.Count)
    {
      return false;
    }

    List<OrderItem> remaining = [.. existing.Items];

    foreach (var item in requestItems)
    {
      var match = remaining.FirstOrDefault(candidate =>
                                             candidate.CatalogItemId == item.CatalogItemId
                                             && string.Equals(candidate.Note ?? string.Empty, item.Note ?? string.Empty, StringComparison.Ordinal));

      if (match is null)
      {
        return false;
      }

      remaining.Remove(match);
    }

    return true;
  }
}
