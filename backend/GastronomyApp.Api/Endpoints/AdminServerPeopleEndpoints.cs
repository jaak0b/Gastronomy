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

public static class AdminServerPeopleEndpoints
{
    public static IEndpointRouteBuilder MapAdminServerPeopleEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/api/admin/server-people");

        group.MapGet(string.Empty, async (
            AdminServerPeopleHandler handler,
            CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

        group.MapPut("/{serverPersonId:guid}", async (
            Guid serverPersonId,
            RenameServerPersonRequest request,
            AdminServerPeopleHandler handler,
            CancellationToken cancellationToken) =>
                await handler.RenameAsync(serverPersonId, request, cancellationToken));

        group.MapPost("/{serverPersonId:guid}/revoke-device", async (
            Guid serverPersonId,
            AdminServerPeopleHandler handler,
            CancellationToken cancellationToken) =>
                await handler.RevokeDeviceAsync(serverPersonId, cancellationToken));

        group.MapPost("/{serverPersonId:guid}/deactivate", async (
            Guid serverPersonId,
            AdminServerPeopleHandler handler,
            CancellationToken cancellationToken) =>
                await handler.DeactivateAsync(serverPersonId, cancellationToken));

        routes.MapPost("/api/admin/enrolment/invitations", async (
            CreateInvitationRequest request,
            AdminServerPeopleHandler handler,
            CancellationToken cancellationToken) => await handler.CreateInvitationAsync(request, cancellationToken));

        return routes;
    }
}

public sealed class AdminServerPeopleHandler
{
    private readonly GastronomyAppDbContext dbContext;
    private readonly IDeviceTokenStore deviceTokenStore;
    private readonly IEnrolmentInvitationStore invitationStore;
    private readonly BreakGlassUrlBuilder urlBuilder;
    private readonly OutstandingInvitationCache invitationCache;
    private readonly HubNotificationDispatcher dispatcher;
    private readonly DeviceConnectionTerminator connectionTerminator;
    private readonly ResultEnvelope resultEnvelope;
    private readonly IClock clock;

    public AdminServerPeopleHandler(
        GastronomyAppDbContext dbContext,
        IDeviceTokenStore deviceTokenStore,
        IEnrolmentInvitationStore invitationStore,
        BreakGlassUrlBuilder urlBuilder,
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
        List<ServerPerson> people = await dbContext.ServerPeople
            .AsNoTracking()
            .OrderBy(person => person.Name)
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

        List<AdminServerPersonView> views = [];

        foreach (ServerPerson person in people)
        {
            Device? device = devices.FirstOrDefault(candidate => candidate.ServerPersonId == person.Id);

            views.Add(new AdminServerPersonView(
                person.Id,
                person.Name,
                person.IsActive,
                device is not null,
                device?.LastSeenAtUtc,
                device?.UserAgentSnapshot,
                outstanding.Any(invitation => invitation.ServerPersonId == person.Id)));
        }

        return Results.Ok(new AdminServerPersonListView(views));
    }

    public async Task<IResult> RenameAsync(
        Guid serverPersonId,
        RenameServerPersonRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return resultEnvelope.Problem(
                StatusCodes.Status400BadRequest,
                "ValidationFailed",
                "admin.personNameMissing");
        }

        ServerPerson? person = await dbContext.ServerPeople
            .FirstOrDefaultAsync(candidate => candidate.Id == serverPersonId, cancellationToken);

        if (person is null)
        {
            return Results.NotFound();
        }

        person.Name = request.Name;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ServerPersonView(person.Id, person.Name));
    }

    public async Task<IResult> RevokeDeviceAsync(Guid serverPersonId, CancellationToken cancellationToken)
    {
        Device? device = await dbContext.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.ServerPersonId == serverPersonId && candidate.RevokedAtUtc == null,
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

    public async Task<IResult> DeactivateAsync(Guid serverPersonId, CancellationToken cancellationToken)
    {
        ServerPerson? person = await dbContext.ServerPeople
            .FirstOrDefaultAsync(candidate => candidate.Id == serverPersonId, cancellationToken);

        if (person is null)
        {
            return Results.NotFound();
        }

        List<Device> devices = await dbContext.Devices
            .AsNoTracking()
            .Where(device => device.ServerPersonId == serverPersonId && device.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        person.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (Device device in devices)
        {
            await RevokeAsync(device.Id, cancellationToken);
        }

        return Results.Ok(new ServerPersonView(person.Id, person.Name));
    }

    public async Task<IResult> CreateInvitationAsync(
        CreateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        ServerPerson? person = null;

        if (request.ServerPersonId is not null)
        {
            person = await dbContext.ServerPeople
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == request.ServerPersonId, cancellationToken);

            if (person is null)
            {
                return Results.NotFound();
            }
        }

        EnrolmentInvitationCreated created =
            await invitationStore.CreateAsync(request.ServerPersonId, cancellationToken);

        string qrUrl = urlBuilder.BuildEnrolmentUrl(created.QrCodeValue);
        invitationCache.Remember(new OutstandingInvitation(
            created.InvitationId,
            created.QrCodeValue,
            qrUrl,
            created.ExpiresAtUtc));

        if (request.ServerPersonId is not null)
        {
            List<Device> devices = await dbContext.Devices
                .AsNoTracking()
                .Where(device => device.ServerPersonId == request.ServerPersonId && device.RevokedAtUtc == null)
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
                person is null ? null : new ServerPersonView(person.Id, person.Name),
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
