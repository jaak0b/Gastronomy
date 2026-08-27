using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Auth;

public sealed record StationCaller(Guid ProductionLocationId, string AccessKey);

public sealed class StationCallerAccessor
{
    private const string ItemKey = "station_caller";

    public void Remember(HttpContext context, StationCaller caller)
    {
        context.Items[ItemKey] = caller;
    }

    public StationCaller Read(HttpContext context)
    {
        return (StationCaller)context.Items[ItemKey]!;
    }
}

public sealed class StationAccessKeyMiddleware : IMiddleware
{
    private const string ApiStationPrefix = "/api/station";
    private const string ShellStationPrefix = "/station";

    private readonly StationCallerAccessor callerAccessor;

    public StationAccessKeyMiddleware(StationCallerAccessor callerAccessor)
    {
        this.callerAccessor = callerAccessor;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        string? accessKey = ReadAccessKey(context.Request.Path);

        if (accessKey is null)
        {
            await next(context);
            return;
        }

        GastronomyAppDbContext dbContext = context.RequestServices.GetRequiredService<GastronomyAppDbContext>();

        ProductionLocation? location = await dbContext.ProductionLocations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.StationAccessKey == accessKey && candidate.IsActive,
                context.RequestAborted);

        if (location is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        callerAccessor.Remember(context, new StationCaller(location.Id, accessKey));
        await next(context);
    }

    private string? ReadAccessKey(PathString path)
    {
        if (path.StartsWithSegments(ApiStationPrefix, out PathString apiRemainder))
        {
            return FirstSegment(apiRemainder);
        }

        if (path.StartsWithSegments(ShellStationPrefix, out PathString shellRemainder))
        {
            return FirstSegment(shellRemainder);
        }

        return null;
    }

    private string? FirstSegment(PathString remainder)
    {
        string value = remainder.Value ?? string.Empty;
        string[] segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length == 0 ? null : segments[0];
    }
}
