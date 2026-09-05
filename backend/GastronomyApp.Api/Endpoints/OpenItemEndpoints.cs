using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class OpenItemEndpoints
{
  public static IEndpointRouteBuilder MapOpenItemEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/open-items")
                      .RequireAuthorization()
                      .RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

    group.MapGet(string.Empty,
                 async (OpenItemQueryHandler handler, CancellationToken cancellationToken) =>
                 {
                   return await handler.ListAsync(cancellationToken);
                 });

    group.MapGet("/table-names",
                 async (OpenItemQueryHandler handler, CancellationToken cancellationToken) =>
                 {
                   return await handler.ListTableNamesAsync(cancellationToken);
                 });

    group.MapPost("/settle",
                  async (SettleItemsRequest request,
                         HttpContext httpContext,
                         CallerIdentity callerIdentity,
                         OrderItemSettlementHandler handler,
                         CancellationToken cancellationToken) =>
                  {
                    var caller = callerIdentity.ReadDevice(httpContext.User)!;
                    return await handler.SettleAtTheDisplayedPriceAsync(request, caller, cancellationToken);
                  });

    group.MapPost("/settle-free-of-charge",
                  async (SettleItemsFreeOfChargeRequest request,
                         HttpContext httpContext,
                         CallerIdentity callerIdentity,
                         OrderItemSettlementHandler handler,
                         CancellationToken cancellationToken) =>
                  {
                    var caller = callerIdentity.ReadDevice(httpContext.User)!;
                    return await handler.SettleFreeOfChargeAsync(request, caller, cancellationToken);
                  });

    return routes;
  }
}
