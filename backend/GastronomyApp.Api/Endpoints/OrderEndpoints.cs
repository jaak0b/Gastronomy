using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.RateLimiting;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.Printing;
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
    RouteGroupBuilder group = routes.MapGroup("/api/orders").RequireAuthorization().RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

    group.MapPost(string.Empty, async (
        PlaceOrderRequest request,
        HttpContext httpContext,
        CallerIdentity callerIdentity,
        OrderPlacementHandler handler,
        CancellationToken cancellationToken) =>
    {
      DeviceCaller caller = callerIdentity.ReadDevice(httpContext.User)!;
      return await handler.PlaceAsync(request, caller, cancellationToken);
    });

    group.MapGet("/mine", async (
        HttpContext httpContext,
        CallerIdentity callerIdentity,
        OrderQueryHandler handler,
        CancellationToken cancellationToken) =>
    {
      DeviceCaller caller = callerIdentity.ReadDevice(httpContext.User)!;
      return await handler.ListForStaffMemberAsync(caller.StaffMemberId, cancellationToken);
    });

    group.MapGet("/{orderId:guid}", async (
        Guid orderId,
        HttpContext httpContext,
        CallerIdentity callerIdentity,
        OrderQueryHandler handler,
        CancellationToken cancellationToken) =>
    {
      DeviceCaller caller = callerIdentity.ReadDevice(httpContext.User)!;
      return await handler.DetailAsync(orderId, caller.StaffMemberId, cancellationToken);
    });

    group.MapPost("/{orderId:guid}/station-orders/{stationOrderId:guid}/resolve", async (
        Guid orderId,
        Guid stationOrderId,
        ResolveUnknownPrintRequest request,
        HttpContext httpContext,
        CallerIdentity callerIdentity,
        StationOrderActionHandler handler,
        CancellationToken cancellationToken) =>
    {
      DeviceCaller caller = callerIdentity.ReadDevice(httpContext.User)!;
      return await handler.ResolveUnknownAsync(
              orderId,
              stationOrderId,
              request,
              caller.StaffMemberId,
              cancellationToken);
    });

    group.MapPost("/{orderId:guid}/station-orders/{stationOrderId:guid}/print-another-copy", async (
        Guid orderId,
        Guid stationOrderId,
        HttpContext httpContext,
        CallerIdentity callerIdentity,
        StationOrderActionHandler handler,
        CancellationToken cancellationToken) =>
    {
      DeviceCaller caller = callerIdentity.ReadDevice(httpContext.User)!;
      return await handler.PrintAnotherCopyAsync(
              orderId,
              stationOrderId,
              caller.StaffMemberId,
              cancellationToken);
    });

    return routes;
  }
}

public sealed class OrderPlacementHandler
{
  private readonly GastronomyAppDbContext dbContext;
  private readonly OrderAcceptanceTransaction acceptanceTransaction;
  private readonly OrderReader orderReader;
  private readonly PrintJobEnqueuer printJobEnqueuer;
  private readonly HubNotificationDispatcher dispatcher;
  private readonly ResultEnvelope resultEnvelope;

  public OrderPlacementHandler(
      GastronomyAppDbContext dbContext,
      OrderAcceptanceTransaction acceptanceTransaction,
      OrderReader orderReader,
      PrintJobEnqueuer printJobEnqueuer,
      HubNotificationDispatcher dispatcher,
      ResultEnvelope resultEnvelope)
  {
    this.dbContext = dbContext;
    this.acceptanceTransaction = acceptanceTransaction;
    this.orderReader = orderReader;
    this.printJobEnqueuer = printJobEnqueuer;
    this.dispatcher = dispatcher;
    this.resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> PlaceAsync(
      PlaceOrderRequest request,
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
                    StationId = item.StationId,
                }),
            ],
    };

    Result<OrderAcceptanceResult, OrderValidationFailure> acceptance =
        await acceptanceTransaction.AcceptAsync(acceptanceRequest, cancellationToken);

    if (!acceptance.IsSuccess)
    {
      return resultEnvelope.ToResult(resultEnvelope.Describe(acceptance.Failure));
    }

    Guid orderId = acceptance.Value.Order.Id;

    if (acceptance.Value.WasAlreadyAccepted)
    {
      LoadedOrder? existing = await orderReader.LoadAsync(dbContext, orderId, cancellationToken);

      if (existing is null)
      {
        return resultEnvelope.Problem(
            StatusCodes.Status409Conflict,
            "SubmissionIdReused",
            "order.submissionIdReused");
      }

      if (!new SubmissionComparison().Matches(request, existing))
      {
        return resultEnvelope.Problem(
            StatusCodes.Status409Conflict,
            "SubmissionIdReused",
            "order.submissionIdReused");
      }

      foreach (StationOrder waiting in existing.StationOrders)
      {
        await printJobEnqueuer.EnqueueWithoutFailingTheCallerAsync(waiting.Id, cancellationToken);
      }

      return Results.Json(
          orderReader.Describe(existing),
          statusCode: StatusCodes.Status200OK);
    }

    LoadedOrder placed = (await orderReader.LoadAsync(dbContext, orderId, cancellationToken))!;

    foreach (StationOrder stationOrder in placed.StationOrders)
    {
      await printJobEnqueuer.EnqueueWithoutFailingTheCallerAsync(stationOrder.Id, cancellationToken);
    }

    PlacedOrderView view = orderReader.Describe(placed);

    await dispatcher.PushOrderAcceptedAsync(
        caller.StaffMemberId,
        new OrderAcceptedEvent(
            view.OrderId,
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

    foreach (OrderItemRequest item in requestItems)
    {
      OrderItem? match = remaining.FirstOrDefault(candidate =>
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
