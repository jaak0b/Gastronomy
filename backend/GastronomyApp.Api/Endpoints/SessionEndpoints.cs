using GastronomyApp.Api.Auth.Callers;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using GastronomyApp.Api.Names;

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
