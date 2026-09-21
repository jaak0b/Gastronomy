using GastronomyApp.Api.Auth.Conventions;
using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Handlers;
using GastronomyApp.Contracts.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class OrderEndpoints
{
  public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/orders").RequireAuthorization().RequireStaffDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    group.MapPost(string.Empty,
                  async (PlaceOrderRequest request, HttpContext httpContext, CallerIdentity callerIdentity, OrderPlacementHandler handler, CancellationToken cancellationToken) =>
                  {
                    var caller = callerIdentity.ReadStaffDevice(httpContext.User)!;
                    return await handler.PlaceAsync(request, caller, cancellationToken);
                  })
         .Produces<PlacedOrderView>(StatusCodes.Status201Created);

    return routes;
  }
}
