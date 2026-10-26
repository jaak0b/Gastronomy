using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Enrolment;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class EnrolmentEndpoints
{
  public static IEndpointRouteBuilder MapEnrolmentEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapPost("/api/enrolment/redeem",
                   async Task<ApiAnswer<RedeemedEnrolmentView>> (RedeemEnrolmentRequest request, HttpContext httpContext, EnrolmentService service, CancellationToken cancellationToken) => await service.RedeemAsync(request, httpContext.Request.Headers.AcceptLanguage.ToString(), cancellationToken))
          .AllowAnonymous()
          .RequireRateLimiting(Names.RateLimitPolicies.PerAddress);

    return routes;
  }
}
