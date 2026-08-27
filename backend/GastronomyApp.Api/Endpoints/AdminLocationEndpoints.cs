using System.Security.Cryptography;
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

        group.MapPost("/{locationId:guid}/regenerate-access-key", async (
            Guid locationId,
            AdminLocationHandler handler,
            CancellationToken cancellationToken) =>
                await handler.RegenerateAccessKeyAsync(locationId, cancellationToken));

        group.MapGet("/{locationId:guid}/station-card", async (
            Guid locationId,
            AdminLocationHandler handler,
            CancellationToken cancellationToken) => await handler.StationCardAsync(locationId, cancellationToken));

        return routes;
    }
}

public sealed class StationAccessKeyGenerator
{
    private const int KeyLengthCharacters = 32;
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    public string Generate()
    {
        char[] key = new char[KeyLengthCharacters];

        for (int position = 0; position < KeyLengthCharacters; position++)
        {
            key[position] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(key);
    }
}

public sealed class BreakGlassUrlBuilder
{
    private readonly ApiHostOptions hostOptions;
    private readonly ReachableHostResolver hostResolver;
    private readonly IServer server;

    public BreakGlassUrlBuilder(ApiHostOptions hostOptions, ReachableHostResolver hostResolver, IServer server)
    {
        this.hostOptions = hostOptions;
        this.hostResolver = hostResolver;
        this.server = server;
    }

    public string Build(string accessKey)
    {
        return $"{Origin()}/station/{accessKey}";
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
    private const string DefaultSlipLanguage = "de";

    private readonly GastronomyAppDbContext dbContext;
    private readonly StationAccessKeyGenerator accessKeyGenerator;
    private readonly BreakGlassUrlBuilder urlBuilder;
    private readonly PrinterFleet printerFleet;
    private readonly ResultEnvelope resultEnvelope;

    public AdminLocationHandler(
        GastronomyAppDbContext dbContext,
        StationAccessKeyGenerator accessKeyGenerator,
        BreakGlassUrlBuilder urlBuilder,
        PrinterFleet printerFleet,
        ResultEnvelope resultEnvelope)
    {
        this.dbContext = dbContext;
        this.accessKeyGenerator = accessKeyGenerator;
        this.urlBuilder = urlBuilder;
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
                location.SlipLanguage,
                location.IsActive,
                location.StationAccessKey,
                urlBuilder.Build(location.StationAccessKey),
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
        string accessKey = accessKeyGenerator.Generate();

        dbContext.ProductionLocations.Add(new ProductionLocation
        {
            Id = locationId,
            Name = request.Name,
            StationAccessKey = accessKey,
            SlipLanguage = request.SlipLanguage ?? DefaultSlipLanguage,
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
            new AccessKeyView(locationId, accessKey, urlBuilder.Build(accessKey)),
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
        location.SlipLanguage = request.SlipLanguage ?? location.SlipLanguage;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AccessKeyView(
            location.Id,
            location.StationAccessKey,
            urlBuilder.Build(location.StationAccessKey)));
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

        return Results.Ok(new AccessKeyView(
            location.Id,
            location.StationAccessKey,
            urlBuilder.Build(location.StationAccessKey)));
    }

    public async Task<IResult> RegenerateAccessKeyAsync(Guid locationId, CancellationToken cancellationToken)
    {
        ProductionLocation? location = await dbContext.ProductionLocations
            .FirstOrDefaultAsync(candidate => candidate.Id == locationId, cancellationToken);

        if (location is null)
        {
            return Results.NotFound();
        }

        location.StationAccessKey = accessKeyGenerator.Generate();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AccessKeyView(
            location.Id,
            location.StationAccessKey,
            urlBuilder.Build(location.StationAccessKey)));
    }

    public async Task<IResult> StationCardAsync(Guid locationId, CancellationToken cancellationToken)
    {
        ProductionLocation? location = await dbContext.ProductionLocations
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == locationId, cancellationToken);

        if (location is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new StationCardView(
            location.Id,
            location.Name,
            urlBuilder.Build(location.StationAccessKey)));
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
