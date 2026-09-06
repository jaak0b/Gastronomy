using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Endpoints;

public static class AdminStaffMembersEndpoints
{
  public static IEndpointRouteBuilder MapAdminStaffMembersEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/staff-members");

    group.MapGet(string.Empty,
                 async (AdminStaffMembersHandler handler,
                        CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    group.MapPost(string.Empty,
                  async (CreateStaffMemberRequest request,
                         AdminStaffMembersHandler handler,
                         CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken));

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

    return routes;
  }
}

public sealed class AdminStaffMembersHandler
{
  private readonly IClock _clock;
  private readonly GastronomyAppDbContext _dbContext;
  private readonly DeviceRevoker _deviceRevoker;
  private readonly OutstandingInvitationLookup _invitationLookup;
  private readonly ResultEnvelope _resultEnvelope;

  public AdminStaffMembersHandler(GastronomyAppDbContext dbContext,
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
    List<StaffMember> staffMembers = await _dbContext.StaffMembers
                                                     .AsNoTracking()
                                                     .OrderBy(staffMember => staffMember.Name)
                                                     .ToListAsync(cancellationToken);

    HashSet<Guid> outstandingInvitationIds =
      await _invitationLookup.InvitationIdsStillOutstandingAsync(cancellationToken);
    Dictionary<Guid, DateTime> lastSeenByDeviceId =
      await _invitationLookup.LastSeenByDeviceIdAsync(cancellationToken);

    List<AdminStaffMemberView> views =
    [
      .. staffMembers.Select(staffMember => new AdminStaffMemberView(staffMember.Id,
                                                                      staffMember.Name,
                                                                      staffMember.IsActive,
                                                                      staffMember.DeviceId is not null,
                                                                      LastSeenOf(lastSeenByDeviceId, staffMember.DeviceId),
                                                                      staffMember.EnrolmentInvitationId is not null
                                                                      && outstandingInvitationIds.Contains(staffMember.EnrolmentInvitationId.Value)))
    ];

    return Results.Ok(new AdminStaffMemberListView(views));
  }

  public async Task<IResult> CreateAsync(CreateStaffMemberRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                    "ValidationFailed",
                                    "admin.personNameMissing");
    }

    StaffMember staffMember = new()
                              {
                                Id = Guid.NewGuid(),
                                Name = request.Name.Trim(),
                                IsActive = true,
                                CreatedAtUtc = _clock.UtcNow
                              };

    _dbContext.StaffMembers.Add(staffMember);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return Results.Json(new StaffMemberView(staffMember.Id, staffMember.Name),
                        statusCode: StatusCodes.Status201Created);
  }

  public async Task<IResult> RenameAsync(Guid staffMemberId,
                                         RenameStaffMemberRequest request,
                                         CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

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

    var deviceId = staffMember.DeviceId;
    staffMember.IsActive = false;
    await ConsumeOutstandingInvitationOfAsync(staffMember, cancellationToken);
    await _dbContext.SaveChangesAsync(cancellationToken);

    if (deviceId is not null)
    {
      await _deviceRevoker.RevokeAsync(deviceId.Value, cancellationToken);
    }

    return Results.Ok(new StaffMemberView(staffMember.Id, staffMember.Name));
  }

  private DateTime? LastSeenOf(Dictionary<Guid, DateTime> lastSeenByDeviceId, Guid? deviceId)
  {
    return deviceId is not null && lastSeenByDeviceId.TryGetValue(deviceId.Value, out var lastSeen)
             ? lastSeen
             : null;
  }

  private async Task ConsumeOutstandingInvitationOfAsync(StaffMember staffMember, CancellationToken cancellationToken)
  {
    if (staffMember.EnrolmentInvitationId is null)
    {
      return;
    }

    var invitation = await _dbContext.EnrolmentInvitations
                                     .FirstOrDefaultAsync(candidate => candidate.Id == staffMember.EnrolmentInvitationId.Value,
                                                          cancellationToken);

    if (invitation is not null && invitation.ConsumedAtUtc is null)
    {
      invitation.ConsumedAtUtc = _clock.UtcNow;
    }

    staffMember.EnrolmentInvitationId = null;
  }
}
