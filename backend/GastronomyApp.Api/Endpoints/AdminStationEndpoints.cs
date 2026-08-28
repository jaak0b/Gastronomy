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

public static class AdminStationEndpoints
{
  public static IEndpointRouteBuilder MapAdminStationEndpoints(this IEndpointRouteBuilder routes)
  {
    RouteGroupBuilder group = routes.MapGroup("/api/admin/stations");

    group.MapGet(string.Empty, async (
        AdminStationHandler handler,
        CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    group.MapPost(string.Empty, async (
        SaveStationRequest request,
        AdminStationHandler handler,
        CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken));

    group.MapPut("/{stationId:guid}", async (
        Guid stationId,
        SaveStationRequest request,
        AdminStationHandler handler,
        CancellationToken cancellationToken) => await handler.UpdateAsync(stationId, request, cancellationToken));

    group.MapPost("/{stationId:guid}/deactivate", async (
        Guid stationId,
        AdminStationHandler handler,
        CancellationToken cancellationToken) => await handler.DeactivateAsync(stationId, cancellationToken));

    group.MapPost("/{stationId:guid}/activate", async (
        Guid stationId,
        AdminStationHandler handler,
        CancellationToken cancellationToken) => await handler.ActivateAsync(stationId, cancellationToken));

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

public sealed class AdminStationHandler
{

  private readonly GastronomyAppDbContext dbContext;
  private readonly PrinterFleet printerFleet;
  private readonly StationPrinterStatusLookup statusLookup;
  private readonly ResultEnvelope resultEnvelope;

  public AdminStationHandler(
      GastronomyAppDbContext dbContext,
      PrinterFleet printerFleet,
      StationPrinterStatusLookup statusLookup,
      ResultEnvelope resultEnvelope)
  {
    this.dbContext = dbContext;
    this.printerFleet = printerFleet;
    this.statusLookup = statusLookup;
    this.resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    List<Station> stations = await dbContext.Stations
        .AsNoTracking()
        .OrderBy(station => station.SortOrder)
        .ToListAsync(cancellationToken);

    List<Printer> printers = await dbContext.Printers
        .AsNoTracking()
        .ToListAsync(cancellationToken);

    Dictionary<Guid, PrinterStatus> statuses = await statusLookup.ByStationAsync(
        dbContext,
        [.. stations.Select(station => station.Id)],
        cancellationToken);

    List<AdminStationView> views = [];

    foreach (Station station in stations)
    {
      Printer? printer = printers.FirstOrDefault(
          candidate => candidate.Id == station.PrinterId);
      statuses.TryGetValue(station.Id, out PrinterStatus? status);

      views.Add(new AdminStationView(
          station.Id,
          station.Name,
          station.SortOrder,
          station.IsActive,
          station.PrinterId,
          printer?.Name,
          status?.IsOnline ?? false,
          status?.IsPaperEnd ?? false,
          status?.IsCoverOpen ?? false,
          status?.IsFaulty ?? false));
    }

    return Results.Ok(new AdminStationListView(views));
  }

  public async Task<IResult> CreateAsync(SaveStationRequest request, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return resultEnvelope.Problem(
          StatusCodes.Status400BadRequest,
          "ValidationFailed",
          "admin.stationNameMissing");
    }

    Guid stationId = Guid.NewGuid();

    dbContext.Stations.Add(new Station
    {
      Id = stationId,
      Name = request.Name,
      SortOrder = request.SortOrder,
      IsActive = true,
      NextStationOrderNumber = 1,
      PrinterId = request.PrinterId,
    });

    await dbContext.SaveChangesAsync(cancellationToken);
    await printerFleet.ReconcileAsync(cancellationToken);

    return Results.Json(
        new SavedStationView(stationId),
        statusCode: StatusCodes.Status201Created);
  }

  public async Task<IResult> UpdateAsync(
      Guid stationId,
      SaveStationRequest request,
      CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return resultEnvelope.Problem(
          StatusCodes.Status400BadRequest,
          "ValidationFailed",
          "admin.stationNameMissing");
    }

    Station? station = await dbContext.Stations
        .FirstOrDefaultAsync(candidate => candidate.Id == stationId, cancellationToken);

    if (station is null)
    {
      return Results.NotFound();
    }

    station.Name = request.Name;
    station.SortOrder = request.SortOrder;
    station.PrinterId = request.PrinterId;
    await dbContext.SaveChangesAsync(cancellationToken);
    await printerFleet.ReconcileAsync(cancellationToken);

    return Results.Ok(new SavedStationView(station.Id));
  }

  public async Task<IResult> ActivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    Station? station = await dbContext.Stations
        .FirstOrDefaultAsync(candidate => candidate.Id == stationId, cancellationToken);

    if (station is null)
    {
      return Results.NotFound();
    }

    station.IsActive = true;
    await dbContext.SaveChangesAsync(cancellationToken);
    await printerFleet.ReconcileAsync(cancellationToken);

    return Results.Ok(new SavedStationView(station.Id));
  }

  public async Task<IResult> DeactivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    Station? station = await dbContext.Stations
        .FirstOrDefaultAsync(candidate => candidate.Id == stationId, cancellationToken);

    if (station is null)
    {
      return Results.NotFound();
    }

    List<Guid> stationOrderIds = await dbContext.StationOrders
        .AsNoTracking()
        .Where(stationOrder => stationOrder.StationId == stationId)
        .Select(stationOrder => stationOrder.Id)
        .ToListAsync(cancellationToken);

    Dictionary<Guid, LatestPrintJob> latestJobs = await StationScreenDescriber.LoadLatestPrintJobsAsync(
        dbContext,
        stationOrderIds,
        cancellationToken);

    int openTickets = latestJobs.Values.Count(job =>
        job.Status != PrintJobStatus.Printed && job.Status != PrintJobStatus.HandledOnPaper);

    if (openTickets > 0)
    {
      return resultEnvelope.Problem(
          StatusCodes.Status409Conflict,
          "StationHasOpenTickets",
          "admin.stationHasOpenTickets",
          new Dictionary<string, string> { ["count"] = openTickets.ToString() });
    }

    List<Guid> strandedItemIds = await StrandedItemIdsAsync(stationId, cancellationToken);

    if (strandedItemIds.Count > 0)
    {
      return resultEnvelope.Problem(
          StatusCodes.Status409Conflict,
          "ItemsWouldHaveNoStation",
          "admin.itemsWouldHaveNoStation",
          new Dictionary<string, string> { ["count"] = strandedItemIds.Count.ToString() });
    }

    station.IsActive = false;
    await dbContext.SaveChangesAsync(cancellationToken);
    await printerFleet.ReconcileAsync(cancellationToken);

    return Results.Ok(new SavedStationView(station.Id));
  }

  private async Task<List<Guid>> StrandedItemIdsAsync(Guid stationId, CancellationToken cancellationToken)
  {
    List<Guid> activeOtherStationIds = await dbContext.Stations
        .AsNoTracking()
        .Where(station => station.IsActive && station.Id != stationId)
        .Select(station => station.Id)
        .ToListAsync(cancellationToken);

    List<Guid> assignedItemIds = await dbContext.ItemStationAssignments
        .AsNoTracking()
        .Where(assignment => assignment.StationId == stationId)
        .Select(assignment => assignment.CatalogItemId)
        .ToListAsync(cancellationToken);

    List<Guid> itemIdsWithAnotherStation = await dbContext.ItemStationAssignments
        .AsNoTracking()
        .Where(assignment => assignedItemIds.Contains(assignment.CatalogItemId)
            && activeOtherStationIds.Contains(assignment.StationId))
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
