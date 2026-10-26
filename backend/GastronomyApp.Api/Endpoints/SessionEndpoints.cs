using GastronomyApp.Api.Answers;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Session;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class SessionEndpoints
{
  public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/session").RequireAuthorization().RequireRateLimiting(Names.RateLimitPolicies.PerDevice);

    group.MapGet(string.Empty, async Task<ApiAnswer<SessionView>> (DeviceCaller caller, DeviceSessionService service, CancellationToken cancellationToken) => await service.ReadAsync(caller.DeviceId, cancellationToken));

    group.MapPut("/language", async Task<NoContentAnswer> (LanguageChangeRequest request, DeviceCaller caller, DeviceSessionService service, CancellationToken cancellationToken) => await service.ChangeLanguageAsync(caller.DeviceId, request, cancellationToken));

    return routes;
  }
}
