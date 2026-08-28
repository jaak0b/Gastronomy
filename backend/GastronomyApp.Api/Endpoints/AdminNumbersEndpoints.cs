using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class AdminNumbersEndpoints
{
    public static void MapAdminNumbersEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/admin/numbers");

        group.MapPost("/reset", async (
            GastronomyAppDbContext dbContext,
            INumberAllocator numberAllocator,
            CancellationToken cancellationToken) =>
        {
            int stationCounters = await dbContext.Stations.CountAsync(cancellationToken);

            await numberAllocator.ResetOrderAndStationNumbersAsync(cancellationToken);

            return Results.Ok(new ResetNumbersView(stationCounters));
        });
    }
}
