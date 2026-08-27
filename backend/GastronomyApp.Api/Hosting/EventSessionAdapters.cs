using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Hosting;

public sealed class EventSessionRepository : IEventSessionRepository
{
    private readonly GastronomyAppDbContext dbContext;

    public EventSessionRepository(GastronomyAppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<EventSession?> FindActiveAsync(CancellationToken cancellationToken)
    {
        return await dbContext.EventSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(session => session.IsActive, cancellationToken);
    }
}

public sealed class EventSessionStartGuardReader : IEventSessionStartGuardReader
{
    private readonly GastronomyAppDbContext dbContext;

    public EventSessionStartGuardReader(GastronomyAppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<LocationTicketStatus>> FindTicketStatusesAsync(
        Guid eventSessionId,
        CancellationToken cancellationToken)
    {
        List<Guid> orderIds = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.EventSessionId == eventSessionId)
            .Select(order => order.Id)
            .ToListAsync(cancellationToken);

        return await dbContext.LocationTickets
            .AsNoTracking()
            .Where(ticket => orderIds.Contains(ticket.OrderId))
            .Select(ticket => ticket.Status)
            .ToListAsync(cancellationToken);
    }

    public async Task<DateTime?> FindMostRecentOrderAcceptedAtUtcAsync(
        Guid eventSessionId,
        CancellationToken cancellationToken)
    {
        List<DateTime> mostRecent = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.EventSessionId == eventSessionId)
            .OrderByDescending(order => order.CreatedAtUtc)
            .Select(order => order.CreatedAtUtc)
            .Take(1)
            .ToListAsync(cancellationToken);

        return mostRecent.Count == 0 ? null : mostRecent[0];
    }

    public async Task<IReadOnlyCollection<ProductionLocation>> FindActiveLocationsOnTestPrinterAsync(
        CancellationToken cancellationToken)
    {
        List<Guid> mockLocationIds = await dbContext.PrinterConfigurations
            .AsNoTracking()
            .Where(configuration => configuration.TransportKind == TransportKind.Mock)
            .Select(configuration => configuration.ProductionLocationId)
            .ToListAsync(cancellationToken);

        return await dbContext.ProductionLocations
            .AsNoTracking()
            .Where(location => location.IsActive && mockLocationIds.Contains(location.Id))
            .OrderBy(location => location.SortOrder)
            .ToListAsync(cancellationToken);
    }
}
