using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Printing;
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
    var group = routes.MapGroup("/api/orders").RequireAuthorization().RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

    group.MapPost(string.Empty,
                  async (PlaceOrderRequest request,
                         HttpContext httpContext,
                         CallerIdentity callerIdentity,
                         OrderPlacementHandler handler,
                         CancellationToken cancellationToken) =>
                  {
                    var caller = callerIdentity.ReadDevice(httpContext.User)!;
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
  private readonly PrintJobEnqueuer _printJobEnqueuer;
  private readonly ResultEnvelope _resultEnvelope;

  public OrderPlacementHandler(GastronomyAppDbContext dbContext,
                               OrderAcceptanceTransaction acceptanceTransaction,
                               OrderReader orderReader,
                               PrintJobEnqueuer printJobEnqueuer,
                               HubNotificationDispatcher dispatcher,
                               ResultEnvelope resultEnvelope)
  {
    _dbContext = dbContext;
    _acceptanceTransaction = acceptanceTransaction;
    _orderReader = orderReader;
    _printJobEnqueuer = printJobEnqueuer;
    _dispatcher = dispatcher;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> PlaceAsync(PlaceOrderRequest request,
                                        DeviceCaller caller,
                                        CancellationToken cancellationToken)
  {
    OrderAcceptanceRequest acceptanceRequest = new()
                                               {
                                                 ClientOrderId = request.ClientOrderId,
                                                 StaffMemberId = caller.StaffMemberId,
                                                 TableName = request.TableName ?? string.Empty,
                                                 Note = request.Note,
                                                 Items =
                                                 [
                                                   .. (request.Items ?? []).Select(item => new OrderAcceptanceItemRequest
                                                                                           {
                                                                                             CatalogItemId = item.CatalogItemId,
                                                                                             UnitPriceCents = item.UnitPriceCents,
                                                                                             Note = item.Note,
                                                                                             StationId = item.StationId
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

      if (existing is null)
      {
        return _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                      "SubmissionIdReused",
                                      "order.submissionIdReused");
      }

      if (!new SubmissionComparison().Matches(request, existing))
      {
        return _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                      "SubmissionIdReused",
                                      "order.submissionIdReused");
      }

      foreach (var waiting in existing.StationOrders)
      {
        await _printJobEnqueuer.EnqueueWithoutFailingTheCallerAsync(waiting.Id, cancellationToken);
      }

      return Results.Json(_orderReader.Describe(existing),
                          statusCode: StatusCodes.Status200OK);
    }

    var placed = (await _orderReader.LoadAsync(_dbContext, orderId, cancellationToken))!;

    foreach (var stationOrder in placed.StationOrders)
    {
      await _printJobEnqueuer.EnqueueWithoutFailingTheCallerAsync(stationOrder.Id, cancellationToken);
    }

    var view = _orderReader.Describe(placed);

    await _dispatcher.PushOrderAcceptedAsync(caller.StaffMemberId,
                                            new(view.OrderId,
                                                view.GlobalOrderNumber,
                                                placed.Order.TableName,
                                                view.TotalCents,
                                                view.StationOrders),
                                            cancellationToken);

    return Results.Json(view, statusCode: StatusCodes.Status201Created);
  }
}

public sealed class SubmissionComparison
{
  public bool Matches(PlaceOrderRequest request, LoadedOrder existing)
  {
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
