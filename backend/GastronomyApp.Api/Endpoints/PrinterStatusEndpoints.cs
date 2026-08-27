using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.RateLimiting;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class PrinterStatusEndpoints
{
    public static IEndpointRouteBuilder MapPrinterStatusEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/printers/status", async (
            PrinterStatusReader reader,
            GastronomyAppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await reader.ReadAsync(dbContext, cancellationToken));
        }).RequireAuthorization().RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

        return routes;
    }
}

public sealed class PrinterStatusReader
{
    public async Task<PrinterStatusListView> ReadAsync(
        GastronomyAppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        List<ProductionLocation> locations = await dbContext.ProductionLocations
            .AsNoTracking()
            .Where(location => location.IsActive)
            .OrderBy(location => location.SortOrder)
            .ToListAsync(cancellationToken);

        HashSet<Guid> locationIds = [.. locations.Select(location => location.Id)];

        Dictionary<Guid, PrinterStatus> statuses = await dbContext.PrinterStatuses
            .AsNoTracking()
            .Where(status => locationIds.Contains(status.ProductionLocationId))
            .ToDictionaryAsync(status => status.ProductionLocationId, cancellationToken);

        List<PrinterStatusView> views = [];

        foreach (ProductionLocation location in locations)
        {
            statuses.TryGetValue(location.Id, out PrinterStatus? status);

            views.Add(new PrinterStatusView(
                location.Id,
                location.Name,
                status?.IsOnline ?? false,
                status?.IsPaperEnd ?? false,
                status?.IsPaperNearEnd ?? false,
                status?.IsCoverOpen ?? false,
                status?.IsFaulty ?? false,
                status?.LastChangedAtUtc ?? default));
        }

        return new PrinterStatusListView(views);
    }
}
