using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Api.Options;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class AdminStationEndpoints
{
  public static IEndpointRouteBuilder MapAdminStationEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/stations");

    group.MapGet(string.Empty,
                 async (AdminStationHandler handler,
                        CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    group.MapPost(string.Empty,
                  async (SaveStationRequest request,
                         AdminStationHandler handler,
                         CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken));

    group.MapPut("/{stationId:guid}",
                 async (Guid stationId,
                        SaveStationRequest request,
                        AdminStationHandler handler,
                        CancellationToken cancellationToken) => await handler.UpdateAsync(stationId, request, cancellationToken));

    group.MapPost("/{stationId:guid}/deactivate",
                  async (Guid stationId,
                         AdminStationHandler handler,
                         CancellationToken cancellationToken) => await handler.DeactivateAsync(stationId, cancellationToken));

    group.MapPost("/{stationId:guid}/activate",
                  async (Guid stationId,
                         AdminStationHandler handler,
                         CancellationToken cancellationToken) => await handler.ActivateAsync(stationId, cancellationToken));

    return routes;
  }
}

public sealed class EnrolmentUrlBuilder
{
  private readonly ApiHostOptions _hostOptions;
  private readonly ReachableHostResolver _hostResolver;
  private readonly IServer _server;

  public EnrolmentUrlBuilder(ApiHostOptions hostOptions, ReachableHostResolver hostResolver, IServer server)
  {
    _hostOptions = hostOptions;
    _hostResolver = hostResolver;
    _server = server;
  }

  public string BuildEnrolmentUrl(string qrCodeValue)
  {
    return $"{Origin()}/j/{qrCodeValue}";
  }

  public IReadOnlyList<string> ReachableAddresses()
  {
    return _hostResolver.ReachableAddresses();
  }

  public string Origin()
  {
    return $"http://{_hostResolver.ResolveHost()}:{ResolvePort()}";
  }

  private int ResolvePort()
  {
    if (_hostOptions.Port != 0)
    {
      return _hostOptions.Port;
    }

    var addresses = _server.Features.Get<IServerAddressesFeature>();
    var boundAddress = addresses?.Addresses.FirstOrDefault();

    return boundAddress is not null && Uri.TryCreate(boundAddress, UriKind.Absolute, out var uri)
             ? uri.Port
             : _hostOptions.Port;
  }
}

public sealed class AdminStationHandler
{
  private readonly IClock _clock;
  private readonly GastronomyAppDbContext _dbContext;
  private readonly DeviceRevoker _deviceRevoker;
  private readonly OutstandingInvitationLookup _invitationLookup;
  private readonly ResultEnvelope _resultEnvelope;

  public AdminStationHandler(GastronomyAppDbContext dbContext,
                             OutstandingInvitationLookup invitationLookup,
                             DeviceRevoker deviceRevoker,
                             ResultEnvelope resultEnvelope,
                             IClock clock)
  {
    _dbContext = dbContext;
    _invitationLookup = invitationLookup;
    _deviceRevoker = deviceRevoker;
    _resultEnvelope = resultEnvelope;
    _clock = clock;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    List<Station> stations = await _dbContext.Stations
                                             .AsNoTracking()
                                             .OrderBy(station => station.SortOrder)
                                             .ToListAsync(cancellationToken);

    HashSet<Guid> outstandingInvitationIds =
      await _invitationLookup.InvitationIdsStillOutstandingAsync(cancellationToken);
    Dictionary<Guid, DateTime> lastSeenByDeviceId =
      await _invitationLookup.LastSeenByDeviceIdAsync(cancellationToken);

    List<AdminStationView> views =
    [
      .. stations.Select(station => new AdminStationView(station.Id,
                                                          station.Name,
                                                          station.SortOrder,
                                                          station.IsActive,
                                                          station.DeviceId is not null,
                                                          LastSeenOf(lastSeenByDeviceId, station.DeviceId),
                                                          station.EnrolmentInvitationId is not null
                                                          && outstandingInvitationIds.Contains(station.EnrolmentInvitationId.Value)))
    ];

    return Results.Ok(new AdminStationListView(views));
  }

  public async Task<IResult> CreateAsync(SaveStationRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                    "ValidationFailed",
                                    "admin.stationNameMissing");
    }

    var stationId = Guid.NewGuid();

    _dbContext.Stations.Add(new()
                           {
                             Id = stationId,
                             Name = request.Name,
                             SortOrder = request.SortOrder,
                             IsActive = true,
                             NextStationOrderNumber = 1
                           });

    await _dbContext.SaveChangesAsync(cancellationToken);

    return Results.Json(new SavedStationView(stationId), statusCode: StatusCodes.Status201Created);
  }

  public async Task<IResult> UpdateAsync(Guid stationId,
                                         SaveStationRequest request,
                                         CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                    "ValidationFailed",
                                    "admin.stationNameMissing");
    }

    var station = await _dbContext.Stations
                                  .FirstOrDefaultAsync(candidate => candidate.Id == stationId, cancellationToken);

    if (station is null)
    {
      return Results.NotFound();
    }

    station.Name = request.Name;
    station.SortOrder = request.SortOrder;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new SavedStationView(station.Id));
  }

  public async Task<IResult> ActivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var station = await _dbContext.Stations
                                  .FirstOrDefaultAsync(candidate => candidate.Id == stationId, cancellationToken);

    if (station is null)
    {
      return Results.NotFound();
    }

    station.IsActive = true;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new SavedStationView(station.Id));
  }

  public async Task<IResult> DeactivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var station = await _dbContext.Stations
                                  .FirstOrDefaultAsync(candidate => candidate.Id == stationId, cancellationToken);

    if (station is null)
    {
      return Results.NotFound();
    }

    var unfinishedItemCount = await UnfinishedItemCountAsync(stationId, cancellationToken);

    if (unfinishedItemCount > 0)
    {
      return _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                    "StationHasUnfinishedItems",
                                    "admin.stationHasUnfinishedItems",
                                    new Dictionary<string, string> { ["count"] = unfinishedItemCount.ToString() });
    }

    List<Guid> strandedItemIds = await StrandedItemIdsAsync(stationId, cancellationToken);

    if (strandedItemIds.Count > 0)
    {
      return _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                    "ItemsWouldHaveNoStation",
                                    "admin.itemsWouldHaveNoStation",
                                    new Dictionary<string, string> { ["count"] = strandedItemIds.Count.ToString() });
    }

    var deviceId = station.DeviceId;
    station.IsActive = false;
    await ConsumeOutstandingInvitationOfAsync(station, cancellationToken);
    await _dbContext.SaveChangesAsync(cancellationToken);

    if (deviceId is not null)
    {
      await _deviceRevoker.RevokeAsync(deviceId.Value, cancellationToken);
    }

    return Results.Ok(new SavedStationView(station.Id));
  }

  private DateTime? LastSeenOf(Dictionary<Guid, DateTime> lastSeenByDeviceId, Guid? deviceId)
  {
    return deviceId is not null && lastSeenByDeviceId.TryGetValue(deviceId.Value, out var lastSeen)
             ? lastSeen
             : null;
  }

  private async Task ConsumeOutstandingInvitationOfAsync(Station station, CancellationToken cancellationToken)
  {
    if (station.EnrolmentInvitationId is null)
    {
      return;
    }

    var invitation = await _dbContext.EnrolmentInvitations
                                     .FirstOrDefaultAsync(candidate => candidate.Id == station.EnrolmentInvitationId.Value,
                                                          cancellationToken);

    if (invitation is not null && invitation.ConsumedAtUtc is null)
    {
      invitation.ConsumedAtUtc = _clock.UtcNow;
    }

    station.EnrolmentInvitationId = null;
  }

  private async Task<int> UnfinishedItemCountAsync(Guid stationId, CancellationToken cancellationToken)
  {
    List<Guid> stationOrderIds = await _dbContext.StationOrders
                                                 .AsNoTracking()
                                                 .Where(stationOrder => stationOrder.StationId == stationId)
                                                 .Select(stationOrder => stationOrder.Id)
                                                 .ToListAsync(cancellationToken);

    return await _dbContext.OrderItems
                           .AsNoTracking()
                           .CountAsync(item => stationOrderIds.Contains(item.StationOrderId)
                                               && item.ProductionStatus != ProductionStatus.Finished,
                                       cancellationToken);
  }

  private async Task<List<Guid>> StrandedItemIdsAsync(Guid stationId, CancellationToken cancellationToken)
  {
    List<Guid> activeOtherStationIds = await _dbContext.Stations
                                                       .AsNoTracking()
                                                       .Where(station => station.IsActive && station.Id != stationId)
                                                       .Select(station => station.Id)
                                                       .ToListAsync(cancellationToken);

    List<Guid> assignedItemIds = await _dbContext.ItemStationAssignments
                                                 .AsNoTracking()
                                                 .Where(assignment => assignment.StationId == stationId)
                                                 .Select(assignment => assignment.CatalogItemId)
                                                 .ToListAsync(cancellationToken);

    List<Guid> itemIdsWithAnotherStation = await _dbContext.ItemStationAssignments
                                                           .AsNoTracking()
                                                           .Where(assignment => assignedItemIds.Contains(assignment.CatalogItemId)
                                                                                && activeOtherStationIds.Contains(assignment.StationId))
                                                           .Select(assignment => assignment.CatalogItemId)
                                                           .Distinct()
                                                           .ToListAsync(cancellationToken);

    List<Guid> activeItemIds = await _dbContext.CatalogItems
                                               .AsNoTracking()
                                               .Where(item => item.IsActive && assignedItemIds.Contains(item.Id))
                                               .Select(item => item.Id)
                                               .ToListAsync(cancellationToken);

    return [.. activeItemIds.Where(itemId => !itemIdsWithAnotherStation.Contains(itemId))];
  }
}
