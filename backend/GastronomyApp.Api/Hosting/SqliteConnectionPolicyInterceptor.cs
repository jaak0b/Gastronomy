using System.Data.Common;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GastronomyApp.Api.Hosting;

public sealed class SqliteConnectionPolicyInterceptor : DbConnectionInterceptor
{
    private readonly SqliteConnectionFactory connectionFactory;

    public SqliteConnectionPolicyInterceptor(SqliteConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        connectionFactory.ApplyConnectionPolicy(connection);

        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await connectionFactory.ApplyConnectionPolicyAsync(connection, cancellationToken);

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}
