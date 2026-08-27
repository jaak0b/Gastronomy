using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Hosting;

public interface ISessionStateQuery
{
    public Task<bool> IsSessionActiveAsync(CancellationToken cancellationToken);
}

public sealed class EventSessionStateQuery : ISessionStateQuery
{
    private readonly IDbContextFactory<GastronomyAppDbContext> contextFactory;

    public EventSessionStateQuery(IDbContextFactory<GastronomyAppDbContext> contextFactory)
    {
        this.contextFactory = contextFactory;
    }

    public async Task<bool> IsSessionActiveAsync(CancellationToken cancellationToken)
    {
        await using GastronomyAppDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.EventSessions.AnyAsync(session => session.IsActive, cancellationToken);
    }
}
