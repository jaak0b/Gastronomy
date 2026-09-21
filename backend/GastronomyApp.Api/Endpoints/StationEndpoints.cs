using GastronomyApp.Api.Auth.Conventions;
using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Handlers;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Contracts.Stations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class StationEndpoints
{
  public static IEndpointRouteBuilder MapStationEndpoints(this IEndpointRouteBuilder routes)
  {
    var estimates = routes.MapGroup("/api/estimates").RequireAuthorization().RequireStaffDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    estimates.MapGet(string.Empty, async (StationEstimateHandler handler, CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken)).Produces<StationEstimateListView>();

    var stationTablet = routes.MapGroup("/api/station").RequireAuthorization().RequireStationDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    stationTablet.MapGet("/orders",
                         async (HttpContext httpContext, CallerIdentity callerIdentity, StationQueueHandler handler, CancellationToken cancellationToken) =>
                         {
                           var caller = callerIdentity.ReadStationDevice(httpContext.User)!;
                           return await handler.ListQueueAsync(caller, cancellationToken);
                         })
                 .Produces<StationQueueView>();

    stationTablet.MapGet("/orders/fulfilled",
                         async (HttpContext httpContext, CallerIdentity callerIdentity, StationQueueHandler handler, CancellationToken cancellationToken) =>
                         {
                           var caller = callerIdentity.ReadStationDevice(httpContext.User)!;
                           return await handler.ListFulfilledAsync(caller, cancellationToken);
                         })
                 .Produces<StationFulfilledView>();

    stationTablet.MapPost("/items/fulfill",
                          async (StationItemSelectionRequest request, HttpContext httpContext, CallerIdentity callerIdentity, StationFulfillmentHandler handler, CancellationToken cancellationToken) =>
                          {
                            var caller = callerIdentity.ReadStationDevice(httpContext.User)!;
                            return await handler.FulfillAsync(request, caller, cancellationToken);
                          })
                 .Produces<StationQueueView>();

    stationTablet.MapPost("/items/unfulfill",
                          async (StationItemSelectionRequest request, HttpContext httpContext, CallerIdentity callerIdentity, StationFulfillmentHandler handler, CancellationToken cancellationToken) =>
                          {
                            var caller = callerIdentity.ReadStationDevice(httpContext.User)!;
                            return await handler.UnfulfillAsync(request, caller, cancellationToken);
                          })
                 .Produces<StationQueueView>();

    stationTablet.MapPost("/orders/{stationOrderId:guid}/hide",
                          async (Guid stationOrderId, HttpContext httpContext, CallerIdentity callerIdentity, StationFulfillmentHandler handler, CancellationToken cancellationToken) =>
                          {
                            var caller = callerIdentity.ReadStationDevice(httpContext.User)!;
                            return await handler.HideAsync(stationOrderId, caller, cancellationToken);
                          })
                 .Produces<StationQueueView>();

    return routes;
  }
}
