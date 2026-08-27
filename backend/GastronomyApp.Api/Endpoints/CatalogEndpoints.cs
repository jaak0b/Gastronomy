using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.RateLimiting;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/catalog", async (
            GastronomyAppDbContext dbContext,
            CatalogReader catalogReader,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await catalogReader.ReadAsync(dbContext, cancellationToken));
        }).RequireAuthorization().RequireRateLimiting(new RateLimitPolicyNames().PerDevice);

        return routes;
    }
}

public sealed class CatalogReader
{
    private readonly TimeProvider timeProvider;

    public CatalogReader(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public async Task<CatalogView> ReadAsync(GastronomyAppDbContext dbContext, CancellationToken cancellationToken)
    {
        List<ProductionLocation> locations = await dbContext.ProductionLocations
            .Where(location => location.IsActive)
            .OrderBy(location => location.SortOrder)
            .ToListAsync(cancellationToken);

        HashSet<Guid> activeLocationIds = [.. locations.Select(location => location.Id)];

        List<CatalogItem> items = await dbContext.CatalogItems
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);

        HashSet<Guid> itemIds = [.. items.Select(item => item.Id)];

        List<ItemLocationAssignment> assignments = await dbContext.ItemLocationAssignments
            .Where(assignment => itemIds.Contains(assignment.CatalogItemId))
            .ToListAsync(cancellationToken);

        List<TableSuggestion> tableSuggestions = await dbContext.TableSuggestions
            .OrderBy(suggestion => suggestion.SortOrder)
            .ToListAsync(cancellationToken);

        List<CatalogItemView> itemViews =
        [
            .. items.Select(item => new CatalogItemView(
                item.Id,
                item.Name,
                item.CategoryName,
                item.PriceCents,
                item.SortOrder,
                item.IsAvailable,
                [
                    .. assignments
                        .Where(assignment =>
                            assignment.CatalogItemId == item.Id
                            && activeLocationIds.Contains(assignment.ProductionLocationId))
                        .Select(assignment => assignment.ProductionLocationId),
                ])),
        ];

        List<CatalogCategoryView> categories =
        [
            .. items
                .GroupBy(item => item.CategoryName)
                .Select(group => new CatalogCategoryView(group.Key, group.Min(item => item.SortOrder)))
                .OrderBy(category => category.SortOrder),
        ];

        return new CatalogView(
            timeProvider.GetUtcNow().ToString("O"),
            categories,
            itemViews,
            [.. locations.Select(location => new CatalogLocationView(location.Id, location.Name, location.SortOrder))],
            [.. tableSuggestions.Select(suggestion => new TableSuggestionView(suggestion.Label, suggestion.SortOrder))]);
    }
}
