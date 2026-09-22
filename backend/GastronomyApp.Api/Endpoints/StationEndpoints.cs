using GastronomyApp.Api.Auth.Conventions;
using GastronomyApp.Api.Handlers;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Stations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class StationEndpoints
{
  public static IEndpointRouteBuilder MapStationEndpoints(this IEndpointRouteBuilder routes)
  {
    var estimates = routes.MapGroup("/api/estimates").RequireAuthorization().RequireStaffDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    estimates.MapGet(string.Empty, async (StationEstimateHandler handler, CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    var stationTablet = routes.MapGroup("/api/station").RequireAuthorization().RequireStationDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    stationTablet.MapGet("/orders", async (StationDeviceCaller caller, StationQueueHandler handler, CancellationToken cancellationToken) => await handler.ListQueueAsync(caller, cancellationToken));

    stationTablet.MapGet("/orders/fulfilled", async (StationDeviceCaller caller, StationQueueHandler handler, CancellationToken cancellationToken) => await handler.ListFulfilledAsync(caller, cancellationToken));

    stationTablet.MapPost("/items/fulfill", async (StationItemSelectionRequest request, StationDeviceCaller caller, StationFulfillmentHandler handler, CancellationToken cancellationToken) => await handler.FulfillAsync(request, caller, cancellationToken));

    stationTablet.MapPost("/items/unfulfill", async (StationItemSelectionRequest request, StationDeviceCaller caller, StationFulfillmentHandler handler, CancellationToken cancellationToken) => await handler.UnfulfillAsync(request, caller, cancellationToken));

    stationTablet.MapPost("/orders/{stationOrderId:guid}/hide", async (Guid stationOrderId, StationDeviceCaller caller, StationFulfillmentHandler handler, CancellationToken cancellationToken) => await handler.HideAsync(stationOrderId, caller, cancellationToken));

    return routes;
  }
}
