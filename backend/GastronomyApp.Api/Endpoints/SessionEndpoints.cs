using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.RateLimiting;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class SessionEndpoints
{
  public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/session").RequireAuthorization().RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

    group.MapGet(string.Empty,
                 async (HttpContext httpContext,
                        CallerIdentity callerIdentity,
                        DeviceOwnerStore ownerStore,
                        CancellationToken cancellationToken) =>
                 {
                   var caller = callerIdentity.ReadDevice(httpContext.User)!;
                   var owner = await ownerStore.FindAsync(new(caller.OwnerKind, caller.OwnerId), cancellationToken);

                   if (owner is null)
                   {
                     return Results.Unauthorized();
                   }

                   return Results.Ok(new SessionView(caller.DeviceId,
                                                     caller.OwnerKind,
                                                     caller.OwnerKind == DeviceOwnerKind.StaffMember
                                                       ? new StaffMemberView(caller.OwnerId, owner.Name)
                                                       : null,
                                                     caller.OwnerKind == DeviceOwnerKind.Station
                                                       ? new StationSummaryView(caller.OwnerId, owner.Name)
                                                       : null,
                                                     caller.Language));
                 });

    group.MapPut("/language",
                 async (LanguageChangeRequest request,
                        HttpContext httpContext,
                        CallerIdentity callerIdentity,
                        ResultEnvelope resultEnvelope,
                        GastronomyAppDbContext dbContext,
                        CancellationToken cancellationToken) =>
                 {
                   if (request.Language is not ("de" or "en"))
                   {
                     return resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                                   "ValidationFailed",
                                                   "session.unsupportedLanguage");
                   }

                   var caller = callerIdentity.ReadDevice(httpContext.User)!;
                   var device = await dbContext.Devices
                                               .FirstOrDefaultAsync(candidate => candidate.Id == caller.DeviceId, cancellationToken);

                   if (device is null)
                   {
                     return Results.Unauthorized();
                   }

                   device.Language = request.Language;
                   await dbContext.SaveChangesAsync(cancellationToken);

                   return Results.NoContent();
                 });

    return routes;
  }
}
