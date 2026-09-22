using GastronomyApp.Api.Auth.Conventions;
using GastronomyApp.Api.Handlers;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class OrderEndpoints
{
  public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/orders").RequireAuthorization().RequireStaffDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    group.MapPost(string.Empty, async (PlaceOrderRequest request, StaffDeviceCaller caller, OrderPlacementHandler handler, CancellationToken cancellationToken) => await handler.PlaceAsync(request, caller, cancellationToken));

    return routes;
  }
}
