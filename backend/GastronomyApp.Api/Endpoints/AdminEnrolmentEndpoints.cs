using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Ports;
using GastronomyApp.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Endpoints;

public static class AdminEnrolmentEndpoints
{
  public static IEndpointRouteBuilder MapAdminEnrolmentEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapPost("/api/admin/enrolment/invitations",
                   async (CreateInvitationRequest request,
                          AdminEnrolmentHandler handler,
                          CancellationToken cancellationToken) => await handler.CreateInvitationAsync(request, cancellationToken));

    var devices = routes.MapGroup("/api/admin/devices");

    devices.MapGet(string.Empty,
                   async (AdminEnrolmentHandler handler,
                          CancellationToken cancellationToken) => await handler.ListDevicesAsync(cancellationToken));

    devices.MapPost("/{deviceId:guid}/revoke",
                    async (Guid deviceId,
                           AdminEnrolmentHandler handler,
                           CancellationToken cancellationToken) => await handler.RevokeDeviceAsync(deviceId, cancellationToken));

    return routes;
  }
}

public sealed class AdminEnrolmentHandler
{
  private readonly GastronomyAppDbContext _dbContext;
  private readonly DeviceRevoker _deviceRevoker;
  private readonly OutstandingInvitationCache _invitationCache;
  private readonly IEnrolmentInvitationStore _invitationStore;
  private readonly ILogger<AdminEnrolmentHandler> _log;
  private readonly DeviceOwnerStore _ownerStore;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly EnrolmentUrlBuilder _urlBuilder;

  public AdminEnrolmentHandler(GastronomyAppDbContext dbContext,
                               IEnrolmentInvitationStore invitationStore,
                               DeviceOwnerStore ownerStore,
                               EnrolmentUrlBuilder urlBuilder,
                               OutstandingInvitationCache invitationCache,
                               DeviceRevoker deviceRevoker,
                               ResultEnvelope resultEnvelope,
                               ILogger<AdminEnrolmentHandler> log)
  {
    _dbContext = dbContext;
    _invitationStore = invitationStore;
    _ownerStore = ownerStore;
    _urlBuilder = urlBuilder;
    _invitationCache = invitationCache;
    _deviceRevoker = deviceRevoker;
    _resultEnvelope = resultEnvelope;
    _log = log;
  }

  public async Task<IResult> CreateInvitationAsync(CreateInvitationRequest request,
                                                   CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (request.StaffMemberId is not null && request.StationId is not null)
    {
      return _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                    "ValidationFailed",
                                    "enrolment.atMostOneOwner");
    }

    var owner = OwnerOf(request);
    var ownerRecord = owner is null ? null : await _ownerStore.FindAsync(owner, cancellationToken);

    if (owner is not null && ownerRecord is null)
    {
      return Results.NotFound();
    }

    var created = await _invitationStore.CreateAsync(owner, cancellationToken);
    var qrUrl = _urlBuilder.BuildEnrolmentUrl(created.QrCodeValue);
    _invitationCache.Remember(new(created.InvitationId, created.QrCodeValue, qrUrl, created.ExpiresAtUtc));

    _log.LogInformation("Enrolment invitation {InvitationId} was created for the {OwnerKind} {OwnerId} "
                        + "at {Origin}, and is valid until {ExpiresAtUtc}. A missing owner means a waiter "
                        + "who types their name when they scan it.",
                        created.InvitationId,
                        owner?.Kind,
                        owner?.Id,
                        _urlBuilder.Origin(),
                        created.ExpiresAtUtc);

    return Results.Json(new InvitationView(created.InvitationId,
                                           qrUrl,
                                           created.ExpiresAtUtc,
                                           owner?.Kind,
                                           owner?.Kind == DeviceOwnerKind.StaffMember
                                             ? new StaffMemberView(owner.Id, ownerRecord!.Name)
                                             : null,
                                           owner?.Kind == DeviceOwnerKind.Station
                                             ? new StationSummaryView(owner.Id, ownerRecord!.Name)
                                             : null,
                                           _urlBuilder.ReachableAddresses()),
                        statusCode: StatusCodes.Status201Created);
  }

  public async Task<IResult> ListDevicesAsync(CancellationToken cancellationToken)
  {
    List<Device> devices = await _dbContext.Devices
                                           .AsNoTracking()
                                           .OrderBy(device => device.CreatedAtUtc)
                                           .ToListAsync(cancellationToken);

    Dictionary<Guid, DeviceOwnerRecord> ownersByDeviceId = await OwnersByDeviceIdAsync(cancellationToken);

    List<Guid> withoutAnOwner = [.. devices.Where(device => !ownersByDeviceId.ContainsKey(device.Id))
                                           .Select(device => device.Id)];

    if (withoutAnOwner.Count > 0)
    {
      _log.LogError("{DeviceCount} devices belong to nobody and are therefore missing from the device list. "
                    + "Their tokens are already refused at sign in. Device ids: {DeviceIds}.",
                    withoutAnOwner.Count,
                    withoutAnOwner);
    }

    List<AdminDeviceView> views =
    [
      .. devices.Where(device => ownersByDeviceId.ContainsKey(device.Id))
                .Select(device => new AdminDeviceView(device.Id,
                                                       ownersByDeviceId[device.Id].Owner.Kind,
                                                       ownersByDeviceId[device.Id].Owner.Id,
                                                       ownersByDeviceId[device.Id].Name,
                                                       device.Language,
                                                       device.CreatedAtUtc,
                                                       device.LastSeenAtUtc))
    ];

    return Results.Ok(new AdminDeviceListView(views));
  }

  public async Task<IResult> RevokeDeviceAsync(Guid deviceId, CancellationToken cancellationToken)
  {
    var deviceExists = await _dbContext.Devices
                                       .AsNoTracking()
                                       .AnyAsync(device => device.Id == deviceId, cancellationToken);

    if (!deviceExists)
    {
      return Results.NotFound();
    }

    await _deviceRevoker.RevokeAsync(deviceId, cancellationToken);

    return Results.Ok(new RevokedDeviceView(deviceId));
  }

  private async Task<Dictionary<Guid, DeviceOwnerRecord>> OwnersByDeviceIdAsync(CancellationToken cancellationToken)
  {
    Dictionary<Guid, DeviceOwnerRecord> ownersByDeviceId = [];

    List<StaffMember> staffMembers = await _dbContext.StaffMembers
                                                     .AsNoTracking()
                                                     .Where(staffMember => staffMember.DeviceId != null)
                                                     .ToListAsync(cancellationToken);

    foreach (var staffMember in staffMembers)
    {
      ownersByDeviceId[staffMember.DeviceId!.Value] = new()
                                                      {
                                                        Owner = new(DeviceOwnerKind.StaffMember, staffMember.Id),
                                                        Name = staffMember.Name,
                                                        IsActive = staffMember.IsActive,
                                                        DeviceId = staffMember.DeviceId,
                                                        EnrolmentInvitationId = staffMember.EnrolmentInvitationId
                                                      };
    }

    List<Station> stations = await _dbContext.Stations
                                             .AsNoTracking()
                                             .Where(station => station.DeviceId != null)
                                             .ToListAsync(cancellationToken);

    foreach (var station in stations)
    {
      ownersByDeviceId[station.DeviceId!.Value] = new()
                                                  {
                                                    Owner = new(DeviceOwnerKind.Station, station.Id),
                                                    Name = station.Name,
                                                    IsActive = station.IsActive,
                                                    DeviceId = station.DeviceId,
                                                    EnrolmentInvitationId = station.EnrolmentInvitationId
                                                  };
    }

    return ownersByDeviceId;
  }

  private DeviceOwner? OwnerOf(CreateInvitationRequest request)
  {
    if (request.StaffMemberId is not null)
    {
      return new(DeviceOwnerKind.StaffMember, request.StaffMemberId.Value);
    }

    if (request.StationId is not null)
    {
      return new(DeviceOwnerKind.Station, request.StationId.Value);
    }

    return null;
  }
}
