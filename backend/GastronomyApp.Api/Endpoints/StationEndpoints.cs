using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Auth.Conventions;
using GastronomyApp.Api.Handlers;
using GastronomyApp.Api.Names;
using GastronomyApp.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class StationEndpoints
{
  public static IEndpointRouteBuilder MapStationEndpoints(this IEndpointRouteBuilder routes)
  {
    var estimates = routes.MapGroup("/api/estimates").RequireAuthorization().RequireStaffDevice().RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

    estimates.MapGet(string.Empty, async (StationEstimateHandler handler, CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    var stationTablet = routes.MapGroup("/api/station").RequireAuthorization().RequireStationDevice().RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

    stationTablet.MapGet("/orders",
                         async (HttpContext httpContext, CallerIdentity callerIdentity, StationQueueHandler handler, CancellationToken cancellationToken) =>
                         {
                           var caller = callerIdentity.ReadStationDevice(httpContext.User)!;
                           return await handler.ListQueueAsync(caller, cancellationToken);
                         });

    stationTablet.MapGet("/orders/fulfilled",
                         async (HttpContext httpContext, CallerIdentity callerIdentity, StationQueueHandler handler, CancellationToken cancellationToken) =>
                         {
                           var caller = callerIdentity.ReadStationDevice(httpContext.User)!;
                           return await handler.ListFulfilledAsync(caller, cancellationToken);
                         });

    stationTablet.MapPost("/items/fulfill",
                          async (StationItemSelectionRequest request, HttpContext httpContext, CallerIdentity callerIdentity, StationFulfillmentHandler handler, CancellationToken cancellationToken) =>
                          {
                            var caller = callerIdentity.ReadStationDevice(httpContext.User)!;
                            return await handler.FulfillAsync(request, caller, cancellationToken);
                          });

    stationTablet.MapPost("/items/unfulfill",
                          async (StationItemSelectionRequest request, HttpContext httpContext, CallerIdentity callerIdentity, StationFulfillmentHandler handler, CancellationToken cancellationToken) =>
                          {
                            var caller = callerIdentity.ReadStationDevice(httpContext.User)!;
                            return await handler.UnfulfillAsync(request, caller, cancellationToken);
                          });

    stationTablet.MapPost("/orders/{stationOrderId:guid}/hide",
                          async (Guid stationOrderId, HttpContext httpContext, CallerIdentity callerIdentity, StationFulfillmentHandler handler, CancellationToken cancellationToken) =>
                          {
                            var caller = callerIdentity.ReadStationDevice(httpContext.User)!;
                            return await handler.HideAsync(stationOrderId, caller, cancellationToken);
                          });

    return routes;
  }
}
