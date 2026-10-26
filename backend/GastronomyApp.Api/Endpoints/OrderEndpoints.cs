using GastronomyApp.Api.Auth.Conventions;
using GastronomyApp.Api.Answers;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class OrderEndpoints
{
  public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/orders").RequireAuthorization().RequireStaffDevice().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    group.MapPost(string.Empty, async Task<CreatedAnswer<PlacedOrderView>> (PlaceOrderRequest request, StaffDeviceCaller caller, OrderPlacementService service, CancellationToken cancellationToken) => await service.PlaceAsync(request, caller.StaffMemberId, cancellationToken));

    return routes;
  }
}
