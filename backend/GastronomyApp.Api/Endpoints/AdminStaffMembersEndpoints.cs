using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Api.Hub;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Endpoints;

public static class AdminStaffMembersEndpoints
{
  public static IEndpointRouteBuilder MapAdminStaffMembersEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/staff-members");

    group.MapGet(string.Empty,
                 async (AdminStaffMembersHandler handler,
                        CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    group.MapPut("/{staffMemberId:guid}",
                 async (Guid staffMemberId,
                        RenameStaffMemberRequest request,
                        AdminStaffMembersHandler handler,
                        CancellationToken cancellationToken) =>
                   await handler.RenameAsync(staffMemberId, request, cancellationToken));

    group.MapPost("/{staffMemberId:guid}/activate",
                  async (Guid staffMemberId,
                         AdminStaffMembersHandler handler,
                         CancellationToken cancellationToken) => await handler.ActivateAsync(staffMemberId, cancellationToken));

    group.MapPost("/{staffMemberId:guid}/deactivate",
                  async (Guid staffMemberId,
                         AdminStaffMembersHandler handler,
                         CancellationToken cancellationToken) =>
                    await handler.DeactivateAsync(staffMemberId, cancellationToken));

    routes.MapPost("/api/admin/enrolment/invitations",
                   async (CreateInvitationRequest request,
                          AdminStaffMembersHandler handler,
                          CancellationToken cancellationToken) => await handler.CreateInvitationAsync(request, cancellationToken));

    return routes;
  }
}

public sealed class AdminStaffMembersHandler
{
  private readonly IClock _clock;
  private readonly DeviceConnectionTerminator _connectionTerminator;
  private readonly GastronomyAppDbContext _dbContext;
  private readonly IDeviceTokenStore _deviceTokenStore;
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly OutstandingInvitationCache _invitationCache;
  private readonly IEnrolmentInvitationStore _invitationStore;
  private readonly ILogger<AdminStaffMembersHandler> _log;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly EnrolmentUrlBuilder _urlBuilder;

  public AdminStaffMembersHandler(GastronomyAppDbContext dbContext,
                                  IDeviceTokenStore deviceTokenStore,
                                  IEnrolmentInvitationStore invitationStore,
                                  EnrolmentUrlBuilder urlBuilder,
                                  OutstandingInvitationCache invitationCache,
                                  HubNotificationDispatcher dispatcher,
                                  DeviceConnectionTerminator connectionTerminator,
                                  ResultEnvelope resultEnvelope,
                                  ILogger<AdminStaffMembersHandler> log,
                                  IClock clock)
  {
    _dbContext = dbContext;
    _deviceTokenStore = deviceTokenStore;
    _invitationStore = invitationStore;
    _urlBuilder = urlBuilder;
    _invitationCache = invitationCache;
    _dispatcher = dispatcher;
    _connectionTerminator = connectionTerminator;
    _resultEnvelope = resultEnvelope;
    _log = log;
    _clock = clock;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    List<StaffMember> staffMembers = await _dbContext.StaffMembers
                                                    .AsNoTracking()
                                                    .OrderBy(staffMember => staffMember.Name)
                                                    .ToListAsync(cancellationToken);

    List<Device> devices = await _dbContext.Devices
                                          .AsNoTracking()
                                          .ToListAsync(cancellationToken);

    var now = _clock.UtcNow;

    List<EnrolmentInvitation> outstanding = await _dbContext.EnrolmentInvitations
                                                           .AsNoTracking()
                                                           .Where(invitation => invitation.ConsumedAtUtc == null && invitation.ExpiresAtUtc > now)
                                                           .ToListAsync(cancellationToken);

    List<AdminStaffMemberView> views = [];

    foreach (var staffMember in staffMembers)
    {
      var device = devices.FirstOrDefault(candidate => candidate.StaffMemberId == staffMember.Id);

      views.Add(new(staffMember.Id,
                    staffMember.Name,
                    staffMember.IsActive,
                    device is not null,
                    device?.LastSeenAtUtc,
                    outstanding.Any(invitation => invitation.StaffMemberId == staffMember.Id)));
    }

    return Results.Ok(new AdminStaffMemberListView(views));
  }

  public async Task<IResult> RenameAsync(Guid staffMemberId,
                                         RenameStaffMemberRequest request,
                                         CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                    "ValidationFailed",
                                    "admin.personNameMissing");
    }

    var staffMember = await _dbContext.StaffMembers
                                     .FirstOrDefaultAsync(candidate => candidate.Id == staffMemberId, cancellationToken);

    if (staffMember is null)
    {
      return Results.NotFound();
    }

    staffMember.Name = request.Name;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new StaffMemberView(staffMember.Id, staffMember.Name));
  }

  public async Task<IResult> ActivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    var staffMember = await _dbContext.StaffMembers
                                     .FirstOrDefaultAsync(candidate => candidate.Id == staffMemberId, cancellationToken);

    if (staffMember is null)
    {
      return Results.NotFound();
    }

    staffMember.IsActive = true;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new StaffMemberView(staffMember.Id, staffMember.Name));
  }

  public async Task<IResult> DeactivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    var staffMember = await _dbContext.StaffMembers
                                     .FirstOrDefaultAsync(candidate => candidate.Id == staffMemberId, cancellationToken);

    if (staffMember is null)
    {
      return Results.NotFound();
    }

    List<Device> devices = await _dbContext.Devices
                                          .AsNoTracking()
                                          .Where(device => device.StaffMemberId == staffMemberId)
                                          .ToListAsync(cancellationToken);

    staffMember.IsActive = false;

    var now = _clock.UtcNow;
    List<EnrolmentInvitation> outstanding = await _dbContext.EnrolmentInvitations
                                                           .Where(invitation => invitation.StaffMemberId == staffMemberId
                                                                                && invitation.ConsumedAtUtc == null)
                                                           .ToListAsync(cancellationToken);

    foreach (var invitation in outstanding)
    {
      invitation.ConsumedAtUtc = now;
    }

    await _dbContext.SaveChangesAsync(cancellationToken);

    foreach (var device in devices)
    {
      await RevokeAsync(device.Id, cancellationToken);
    }

    return Results.Ok(new StaffMemberView(staffMember.Id, staffMember.Name));
  }

  public async Task<IResult> CreateInvitationAsync(CreateInvitationRequest request,
                                                   CancellationToken cancellationToken)
  {
    StaffMember? staffMember = null;

    if (request.StaffMemberId is not null)
    {
      staffMember = await _dbContext.StaffMembers
                                   .AsNoTracking()
                                   .FirstOrDefaultAsync(candidate => candidate.Id == request.StaffMemberId, cancellationToken);

      if (staffMember is null)
      {
        return Results.NotFound();
      }
    }

    var created =
      await _invitationStore.CreateAsync(request.StaffMemberId, cancellationToken);

    var qrUrl = _urlBuilder.BuildEnrolmentUrl(created.QrCodeValue);
    _invitationCache.Remember(new(created.InvitationId,
                                 created.QrCodeValue,
                                 qrUrl,
                                 created.ExpiresAtUtc));

    _log.LogInformation("Enrolment invitation {InvitationId} was created for staff member {StaffMemberId} "
                        + "at {Origin}, and is valid until {ExpiresAtUtc}.",
                        created.InvitationId,
                        request.StaffMemberId,
                        _urlBuilder.Origin(),
                        created.ExpiresAtUtc);

    if (request.StaffMemberId is not null)
    {
      List<Device> devices = await _dbContext.Devices
                                            .AsNoTracking()
                                            .Where(device => device.StaffMemberId == request.StaffMemberId)
                                            .ToListAsync(cancellationToken);

      foreach (var device in devices)
      {
        await RevokeAsync(device.Id, cancellationToken);
      }
    }

    return Results.Json(new InvitationView(created.InvitationId,
                                           qrUrl,
                                           created.ExpiresAtUtc,
                                           staffMember is null ? null : new StaffMemberView(staffMember.Id, staffMember.Name),
                                           _urlBuilder.ReachableAddresses()),
                        statusCode: StatusCodes.Status201Created);
  }

  private async Task RevokeAsync(Guid deviceId, CancellationToken cancellationToken)
  {
    await _deviceTokenStore.RevokeAsync(deviceId, cancellationToken);
    await _dispatcher.PushDeviceRevokedAsync(deviceId, cancellationToken);
    await _connectionTerminator.TerminateAsync(deviceId, cancellationToken);
  }
}
