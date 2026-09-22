using GastronomyApp.Api.Handlers;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Session;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class SessionEndpoints
{
  public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/session").RequireAuthorization().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    group.MapGet(string.Empty, async (DeviceCaller caller, SessionHandler handler, CancellationToken cancellationToken) => await handler.ReadAsync(caller, cancellationToken));

    group.MapPut("/language", async (LanguageChangeRequest request, DeviceCaller caller, SessionHandler handler, CancellationToken cancellationToken) => await handler.ChangeLanguageAsync(request, caller, cancellationToken));

    return routes;
  }
}
