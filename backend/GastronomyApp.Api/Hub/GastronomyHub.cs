using GastronomyApp.Api.Auth;
using GastronomyApp.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Hub;

public sealed record HubGroupNames
{
    public string Devices { get; } = "devices";
    public string Admin { get; } = "admin";
    public string Stations { get; } = "stations";

    public string Person(Guid serverPersonId)
    {
        return $"person:{serverPersonId}";
    }

    public string Device(Guid deviceId)
    {
        return $"device:{deviceId}";
    }
}

public sealed record HubEventNames
{
    public string OrderAccepted { get; } = "OrderAccepted";
    public string TicketStatusChanged { get; } = "TicketStatusChanged";
    public string OrderStatusChanged { get; } = "OrderStatusChanged";
    public string PrinterStatusChanged { get; } = "PrinterStatusChanged";
    public string CatalogChanged { get; } = "CatalogChanged";
    public string EnrolmentCompleted { get; } = "EnrolmentCompleted";
    public string DeviceRevoked { get; } = "DeviceRevoked";
    public string EventSessionStarted { get; } = "EventSessionStarted";
}

public sealed class GastronomyHub : Microsoft.AspNetCore.SignalR.Hub
{
    private const string StationAccessKeyQueryKey = "stationAccessKey";

    private readonly CallerIdentity callerIdentity;
    private readonly LocalAddressSet localAddresses;
    private readonly HubConnectionRegistry connectionRegistry;
    private readonly IDbContextFactory<GastronomyAppDbContext> contextFactory;
    private readonly HubGroupNames groupNames = new();

    public GastronomyHub(
        CallerIdentity callerIdentity,
        LocalAddressSet localAddresses,
        HubConnectionRegistry connectionRegistry,
        IDbContextFactory<GastronomyAppDbContext> contextFactory)
    {
        this.callerIdentity = callerIdentity;
        this.localAddresses = localAddresses;
        this.connectionRegistry = connectionRegistry;
        this.contextFactory = contextFactory;
    }

    public override async Task OnConnectedAsync()
    {
        DeviceCaller? caller = Context.User is null ? null : callerIdentity.ReadDevice(Context.User);
        HttpContext? httpContext = Context.GetHttpContext();
        string? presentedStationKey = ReadStationAccessKey(httpContext);

        if (presentedStationKey is not null && !await StationKeyIsKnownAsync(presentedStationKey))
        {
            Context.Abort();
            return;
        }

        List<string> joinedGroups = [];

        if (caller is not null)
        {
            joinedGroups.Add(groupNames.Person(caller.ServerPersonId));
            joinedGroups.Add(groupNames.Device(caller.DeviceId));
            joinedGroups.Add(groupNames.Devices);
        }

        if (presentedStationKey is not null)
        {
            joinedGroups.Add(groupNames.Stations);
        }

        if (caller is null
            && presentedStationKey is null
            && httpContext is not null
            && localAddresses.Contains(httpContext.Connection.RemoteIpAddress))
        {
            joinedGroups.Add(groupNames.Admin);
        }

        foreach (string group in joinedGroups)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }

        connectionRegistry.Add(new TrackedHubConnection
        {
            ConnectionId = Context.ConnectionId,
            DeviceId = caller?.DeviceId,
            Groups = joinedGroups,
            CallerContext = Context,
        });

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        connectionRegistry.Remove(Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    private string? ReadStationAccessKey(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return null;
        }

        string? presented = httpContext.Request.Query[StationAccessKeyQueryKey];

        return string.IsNullOrWhiteSpace(presented) ? null : presented;
    }

    private async Task<bool> StationKeyIsKnownAsync(string accessKey)
    {
        await using GastronomyAppDbContext context = await contextFactory.CreateDbContextAsync(Context.ConnectionAborted);

        return await context.ProductionLocations
            .AsNoTracking()
            .AnyAsync(
                location => location.StationAccessKey == accessKey && location.IsActive,
                Context.ConnectionAborted);
    }
}
