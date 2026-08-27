using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Api.Printing;
using GastronomyApp.Api.Options;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class AdminLocationEndpoints
{
    public static IEndpointRouteBuilder MapAdminLocationEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/api/admin/locations");

        group.MapGet(string.Empty, async (
            AdminLocationHandler handler,
            CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

        group.MapPost(string.Empty, async (
            SaveLocationRequest request,
            AdminLocationHandler handler,
            CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken));

        group.MapPut("/{locationId:guid}", async (
            Guid locationId,
            SaveLocationRequest request,
            AdminLocationHandler handler,
            CancellationToken cancellationToken) => await handler.UpdateAsync(locationId, request, cancellationToken));

        group.MapPost("/{locationId:guid}/deactivate", async (
            Guid locationId,
            AdminLocationHandler handler,
            CancellationToken cancellationToken) => await handler.DeactivateAsync(locationId, cancellationToken));

        group.MapPost("/{locationId:guid}/activate", async (
            Guid locationId,
            AdminLocationHandler handler,
            CancellationToken cancellationToken) => await handler.ActivateAsync(locationId, cancellationToken));

        return routes;
    }
}

public sealed class EnrolmentUrlBuilder
{
    private readonly ApiHostOptions hostOptions;
    private readonly ReachableHostResolver hostResolver;
    private readonly IServer server;

    public EnrolmentUrlBuilder(ApiHostOptions hostOptions, ReachableHostResolver hostResolver, IServer server)
    {
        this.hostOptions = hostOptions;
        this.hostResolver = hostResolver;
        this.server = server;
    }

    public string BuildEnrolmentUrl(string qrCodeValue)
    {
        return $"{Origin()}/j/{qrCodeValue}";
    }

    public IReadOnlyList<string> ReachableAddresses()
    {
        return hostResolver.ReachableAddresses();
    }

    public string Origin()
    {
        return $"http://{hostResolver.ResolveHost()}:{ResolvePort()}";
    }

    private int ResolvePort()
    {
        if (hostOptions.Port != 0)
        {
            return hostOptions.Port;
        }

        IServerAddressesFeature? addresses = server.Features.Get<IServerAddressesFeature>();
        string? boundAddress = addresses?.Addresses.FirstOrDefault();

        return boundAddress is not null && Uri.TryCreate(boundAddress, UriKind.Absolute, out Uri? uri)
            ? uri.Port
            : hostOptions.Port;
    }
}

public sealed class AdminLocationHandler
{

    private readonly GastronomyAppDbContext dbContext;
    private readonly PrinterFleet printerFleet;
    private readonly ResultEnvelope resultEnvelope;

    public AdminLocationHandler(
        GastronomyAppDbContext dbContext,
        PrinterFleet printerFleet,
        ResultEnvelope resultEnvelope)
    {
        this.dbContext = dbContext;
        this.printerFleet = printerFleet;
        this.resultEnvelope = resultEnvelope;
    }

    public async Task<IResult> ListAsync(CancellationToken cancellationToken)
    {
        List<ProductionLocation> locations = await dbContext.ProductionLocations
            .AsNoTracking()
            .OrderBy(location => location.SortOrder)
            .ToListAsync(cancellationToken);

        List<PrinterConfiguration> configurations = await dbContext.PrinterConfigurations
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        List<PrinterStatus> statuses = await dbContext.PrinterStatuses
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        List<AdminLocationView> views = [];

        foreach (ProductionLocation location in locations)
        {
            PrinterConfiguration? configuration = configurations.FirstOrDefault(
                candidate => candidate.ProductionLocationId == location.Id);
            PrinterStatus? status = statuses.FirstOrDefault(
                candidate => candidate.ProductionLocationId == location.Id);

            views.Add(new AdminLocationView(
                location.Id,
                location.Name,
                location.SortOrder,
                location.IsActive,
                configuration?.TransportKind.ToString() ?? TransportKind.Mock.ToString(),
                configuration?.Host,
                configuration?.Port ?? 0,
                configuration?.IsEnabled ?? false,
                status?.IsOnline ?? false,
                status?.IsPaperEnd ?? false,
                status?.IsCoverOpen ?? false,
                status?.IsFaulty ?? false));
        }

        return Results.Ok(new AdminLocationListView(views));
    }

    public async Task<IResult> CreateAsync(SaveLocationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return resultEnvelope.Problem(
                StatusCodes.Status400BadRequest,
                "ValidationFailed",
                "admin.locationNameMissing");
        }

        Guid locationId = Guid.NewGuid();

        dbContext.ProductionLocations.Add(new ProductionLocation
        {
            Id = locationId,
            Name = request.Name,
            SortOrder = request.SortOrder,
            IsActive = true,
        });

        dbContext.PrinterConfigurations.Add(new PrinterConfiguration
        {
            ProductionLocationId = locationId,
            TransportKind = TransportKind.Mock,
            Host = null,
            Port = 0,
            AgentIdentifier = null,
            CharactersPerLine = 48,
            CodePageName = "PC858",
            ConnectTimeoutSeconds = 3,
            JobTimeoutSeconds = 90,
            HeartbeatSeconds = 10,
            IsEnabled = true,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await printerFleet.ReconcileAsync(cancellationToken);

        return Results.Json(
            new SavedLocationView(locationId),
            statusCode: StatusCodes.Status201Created);
    }

    public async Task<IResult> UpdateAsync(
        Guid locationId,
        SaveLocationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return resultEnvelope.Problem(
                StatusCodes.Status400BadRequest,
                "ValidationFailed",
                "admin.locationNameMissing");
        }

        ProductionLocation? location = await dbContext.ProductionLocations
            .FirstOrDefaultAsync(candidate => candidate.Id == locationId, cancellationToken);

        if (location is null)
        {
            return Results.NotFound();
        }

        location.Name = request.Name;
        location.SortOrder = request.SortOrder;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new SavedLocationView(location.Id));
    }

    public async Task<IResult> ActivateAsync(Guid locationId, CancellationToken cancellationToken)
    {
        ProductionLocation? location = await dbContext.ProductionLocations
            .FirstOrDefaultAsync(candidate => candidate.Id == locationId, cancellationToken);

        if (location is null)
        {
            return Results.NotFound();
        }

        location.IsActive = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        await printerFleet.ReconcileAsync(cancellationToken);

        return Results.Ok(new SavedLocationView(location.Id));
    }

    public async Task<IResult> DeactivateAsync(Guid locationId, CancellationToken cancellationToken)
    {
        ProductionLocation? location = await dbContext.ProductionLocations
            .FirstOrDefaultAsync(candidate => candidate.Id == locationId, cancellationToken);

        if (location is null)
        {
            return Results.NotFound();
        }

        int openTickets = await dbContext.LocationTickets.CountAsync(
            ticket => ticket.ProductionLocationId == locationId
                && ticket.Status != LocationTicketStatus.Printed
                && ticket.Status != LocationTicketStatus.PrintedOnTestPrinter
                && ticket.Status != LocationTicketStatus.HandledOnPaper,
            cancellationToken);

        if (openTickets > 0)
        {
            return resultEnvelope.Problem(
                StatusCodes.Status409Conflict,
                "LocationHasOpenTickets",
                "admin.locationHasOpenTickets",
                new Dictionary<string, string> { ["count"] = openTickets.ToString() });
        }

        List<Guid> strandedItemIds = await StrandedItemIdsAsync(locationId, cancellationToken);

        if (strandedItemIds.Count > 0)
        {
            return resultEnvelope.Problem(
                StatusCodes.Status409Conflict,
                "ItemsWouldHaveNoStation",
                "admin.itemsWouldHaveNoStation",
                new Dictionary<string, string> { ["count"] = strandedItemIds.Count.ToString() });
        }

        location.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        await printerFleet.ReconcileAsync(cancellationToken);

        return Results.Ok(new SavedLocationView(location.Id));
    }

    private async Task<List<Guid>> StrandedItemIdsAsync(Guid locationId, CancellationToken cancellationToken)
    {
        List<Guid> activeOtherLocationIds = await dbContext.ProductionLocations
            .AsNoTracking()
            .Where(location => location.IsActive && location.Id != locationId)
            .Select(location => location.Id)
            .ToListAsync(cancellationToken);

        List<Guid> assignedItemIds = await dbContext.ItemLocationAssignments
            .AsNoTracking()
            .Where(assignment => assignment.ProductionLocationId == locationId)
            .Select(assignment => assignment.CatalogItemId)
            .ToListAsync(cancellationToken);

        List<Guid> itemIdsWithAnotherStation = await dbContext.ItemLocationAssignments
            .AsNoTracking()
            .Where(assignment => assignedItemIds.Contains(assignment.CatalogItemId)
                && activeOtherLocationIds.Contains(assignment.ProductionLocationId))
            .Select(assignment => assignment.CatalogItemId)
            .Distinct()
            .ToListAsync(cancellationToken);

        List<Guid> activeItemIds = await dbContext.CatalogItems
            .AsNoTracking()
            .Where(item => item.IsActive && assignedItemIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        return [.. activeItemIds.Where(itemId => !itemIdsWithAnotherStation.Contains(itemId))];
    }
}
