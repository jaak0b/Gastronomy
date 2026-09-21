using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Handlers;
using GastronomyApp.Api.Names;
using GastronomyApp.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class SessionEndpoints
{
  public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/session").RequireAuthorization().RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

    group.MapGet(string.Empty,
                 async (HttpContext httpContext, CallerIdentity callerIdentity, SessionHandler handler, CancellationToken cancellationToken) =>
                 {
                   var caller = callerIdentity.ReadDevice(httpContext.User)!;
                   return await handler.ReadAsync(caller, cancellationToken);
                 });

    group.MapPut("/language",
                 async (LanguageChangeRequest request, HttpContext httpContext, CallerIdentity callerIdentity, SessionHandler handler, CancellationToken cancellationToken) =>
                 {
                   var caller = callerIdentity.ReadDevice(httpContext.User)!;
                   return await handler.ChangeLanguageAsync(request, caller, cancellationToken);
                 });

    return routes;
  }
}
