using GastronomyApp.Api.Auth;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.RateLimiting;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/api/session").RequireAuthorization().RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

        group.MapGet(string.Empty, async (
            HttpContext httpContext,
            CallerIdentity callerIdentity,
            GastronomyAppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            DeviceCaller caller = callerIdentity.ReadDevice(httpContext.User)!;

            StaffMember? staffMember = await dbContext.StaffMembers
                .FirstOrDefaultAsync(candidate => candidate.Id == caller.StaffMemberId, cancellationToken);

            if (staffMember is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new SessionView(
                caller.DeviceId,
                new StaffMemberView(staffMember.Id, staffMember.Name),
                caller.Language));
        });

        group.MapPut("/language", async (
            LanguageChangeRequest request,
            HttpContext httpContext,
            CallerIdentity callerIdentity,
            ResultEnvelope resultEnvelope,
            GastronomyAppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (request.Language is not ("de" or "en"))
            {
                return resultEnvelope.Problem(
                    StatusCodes.Status400BadRequest,
                    "ValidationFailed",
                    "session.unsupportedLanguage");
            }

            DeviceCaller caller = callerIdentity.ReadDevice(httpContext.User)!;
            Device? device = await dbContext.Devices
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
