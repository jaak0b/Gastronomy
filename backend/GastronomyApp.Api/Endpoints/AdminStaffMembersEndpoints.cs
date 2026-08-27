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

namespace GastronomyApp.Api.Endpoints;

public static class AdminStaffMembersEndpoints
{
    public static IEndpointRouteBuilder MapAdminStaffMembersEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/api/admin/staff-members");

        group.MapGet(string.Empty, async (
            AdminStaffMembersHandler handler,
            CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

        group.MapPut("/{staffMemberId:guid}", async (
            Guid staffMemberId,
            RenameStaffMemberRequest request,
            AdminStaffMembersHandler handler,
            CancellationToken cancellationToken) =>
                await handler.RenameAsync(staffMemberId, request, cancellationToken));

        group.MapPost("/{staffMemberId:guid}/revoke-device", async (
            Guid staffMemberId,
            AdminStaffMembersHandler handler,
            CancellationToken cancellationToken) =>
                await handler.RevokeDeviceAsync(staffMemberId, cancellationToken));

        group.MapPost("/{staffMemberId:guid}/activate", async (
            Guid staffMemberId,
            AdminStaffMembersHandler handler,
            CancellationToken cancellationToken) => await handler.ActivateAsync(staffMemberId, cancellationToken));

        group.MapPost("/{staffMemberId:guid}/deactivate", async (
            Guid staffMemberId,
            AdminStaffMembersHandler handler,
            CancellationToken cancellationToken) =>
                await handler.DeactivateAsync(staffMemberId, cancellationToken));

        routes.MapPost("/api/admin/enrolment/invitations", async (
            CreateInvitationRequest request,
            AdminStaffMembersHandler handler,
            CancellationToken cancellationToken) => await handler.CreateInvitationAsync(request, cancellationToken));

        return routes;
    }
}

public sealed class AdminStaffMembersHandler
{
    private readonly GastronomyAppDbContext dbContext;
    private readonly IDeviceTokenStore deviceTokenStore;
    private readonly IEnrolmentInvitationStore invitationStore;
    private readonly EnrolmentUrlBuilder urlBuilder;
    private readonly OutstandingInvitationCache invitationCache;
    private readonly HubNotificationDispatcher dispatcher;
    private readonly DeviceConnectionTerminator connectionTerminator;
    private readonly ResultEnvelope resultEnvelope;
    private readonly IClock clock;

    public AdminStaffMembersHandler(
        GastronomyAppDbContext dbContext,
        IDeviceTokenStore deviceTokenStore,
        IEnrolmentInvitationStore invitationStore,
        EnrolmentUrlBuilder urlBuilder,
        OutstandingInvitationCache invitationCache,
        HubNotificationDispatcher dispatcher,
        DeviceConnectionTerminator connectionTerminator,
        ResultEnvelope resultEnvelope,
        IClock clock)
    {
        this.dbContext = dbContext;
        this.deviceTokenStore = deviceTokenStore;
        this.invitationStore = invitationStore;
        this.urlBuilder = urlBuilder;
        this.invitationCache = invitationCache;
        this.dispatcher = dispatcher;
        this.connectionTerminator = connectionTerminator;
        this.resultEnvelope = resultEnvelope;
        this.clock = clock;
    }

    public async Task<IResult> ListAsync(CancellationToken cancellationToken)
    {
        List<StaffMember> staffMembers = await dbContext.StaffMembers
            .AsNoTracking()
            .OrderBy(staffMember => staffMember.Name)
            .ToListAsync(cancellationToken);

        List<Device> devices = await dbContext.Devices
            .AsNoTracking()
            .Where(device => device.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        DateTime now = clock.UtcNow;

        List<EnrolmentInvitation> outstanding = await dbContext.EnrolmentInvitations
            .AsNoTracking()
            .Where(invitation => invitation.ConsumedAtUtc == null && invitation.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);

        List<AdminStaffMemberView> views = [];

        foreach (StaffMember staffMember in staffMembers)
        {
            Device? device = devices.FirstOrDefault(candidate => candidate.StaffMemberId == staffMember.Id);

            views.Add(new AdminStaffMemberView(
                staffMember.Id,
                staffMember.Name,
                staffMember.IsActive,
                device is not null,
                device?.LastSeenAtUtc,
                device?.UserAgentSnapshot,
                outstanding.Any(invitation => invitation.StaffMemberId == staffMember.Id)));
        }

        return Results.Ok(new AdminStaffMemberListView(views));
    }

    public async Task<IResult> RenameAsync(
        Guid staffMemberId,
        RenameStaffMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return resultEnvelope.Problem(
                StatusCodes.Status400BadRequest,
                "ValidationFailed",
                "admin.personNameMissing");
        }

        StaffMember? staffMember = await dbContext.StaffMembers
            .FirstOrDefaultAsync(candidate => candidate.Id == staffMemberId, cancellationToken);

        if (staffMember is null)
        {
            return Results.NotFound();
        }

        staffMember.Name = request.Name;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new StaffMemberView(staffMember.Id, staffMember.Name));
    }

    public async Task<IResult> RevokeDeviceAsync(Guid staffMemberId, CancellationToken cancellationToken)
    {
        Device? device = await dbContext.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.StaffMemberId == staffMemberId && candidate.RevokedAtUtc == null,
                cancellationToken);

        if (device is null)
        {
            return resultEnvelope.Problem(
                StatusCodes.Status409Conflict,
                "PersonHasNoPhone",
                "admin.personHasNoPhone");
        }

        await RevokeAsync(device.Id, cancellationToken);

        return Results.Ok(new DeviceRevokedEvent(device.Id));
    }

    public async Task<IResult> ActivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
    {
        StaffMember? staffMember = await dbContext.StaffMembers
            .FirstOrDefaultAsync(candidate => candidate.Id == staffMemberId, cancellationToken);

        if (staffMember is null)
        {
            return Results.NotFound();
        }

        staffMember.IsActive = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new StaffMemberView(staffMember.Id, staffMember.Name));
    }

    public async Task<IResult> DeactivateAsync(Guid staffMemberId, CancellationToken cancellationToken)
    {
        StaffMember? staffMember = await dbContext.StaffMembers
            .FirstOrDefaultAsync(candidate => candidate.Id == staffMemberId, cancellationToken);

        if (staffMember is null)
        {
            return Results.NotFound();
        }

        List<Device> devices = await dbContext.Devices
            .AsNoTracking()
            .Where(device => device.StaffMemberId == staffMemberId && device.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        staffMember.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (Device device in devices)
        {
            await RevokeAsync(device.Id, cancellationToken);
        }

        return Results.Ok(new StaffMemberView(staffMember.Id, staffMember.Name));
    }

    public async Task<IResult> CreateInvitationAsync(
        CreateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        StaffMember? staffMember = null;

        if (request.StaffMemberId is not null)
        {
            staffMember = await dbContext.StaffMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == request.StaffMemberId, cancellationToken);

            if (staffMember is null)
            {
                return Results.NotFound();
            }
        }

        EnrolmentInvitationCreated created =
            await invitationStore.CreateAsync(request.StaffMemberId, cancellationToken);

        string qrUrl = urlBuilder.BuildEnrolmentUrl(created.QrCodeValue);
        invitationCache.Remember(new OutstandingInvitation(
            created.InvitationId,
            created.QrCodeValue,
            qrUrl,
            created.ExpiresAtUtc));

        if (request.StaffMemberId is not null)
        {
            List<Device> devices = await dbContext.Devices
                .AsNoTracking()
                .Where(device => device.StaffMemberId == request.StaffMemberId && device.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (Device device in devices)
            {
                await RevokeAsync(device.Id, cancellationToken);
            }
        }

        return Results.Json(
            new InvitationView(
                created.InvitationId,
                qrUrl,
                created.SixDigitCode,
                created.ExpiresAtUtc,
                staffMember is null ? null : new StaffMemberView(staffMember.Id, staffMember.Name),
                urlBuilder.ReachableAddresses()),
            statusCode: StatusCodes.Status201Created);
    }

    private async Task RevokeAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        await deviceTokenStore.RevokeAsync(deviceId, cancellationToken);
        await dispatcher.PushDeviceRevokedAsync(deviceId, cancellationToken);
        await connectionTerminator.TerminateAsync(deviceId, cancellationToken);
    }
}
