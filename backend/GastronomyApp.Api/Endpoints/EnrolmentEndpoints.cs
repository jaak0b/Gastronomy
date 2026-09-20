using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.Handlers;
using GastronomyApp.Api.Names;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class EnrolmentEndpoints
{
  public static IEndpointRouteBuilder MapEnrolmentEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapPost("/api/enrolment/redeem", async (RedeemEnrolmentRequest request, HttpContext httpContext, EnrolmentRedemptionHandler handler, CancellationToken cancellationToken) => await handler.RedeemAsync(request, httpContext, cancellationToken))
          .AllowAnonymous()
          .RequireRateLimiting(new RateLimitPolicyNames().PerAddress);

    return routes;
  }
}
