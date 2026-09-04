using System.Data.Common;
using GastronomyApp.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GastronomyApp.Api.Hosting;

public sealed class SqliteConnectionPolicyInterceptor : DbConnectionInterceptor
{
  private readonly SqliteConnectionFactory _connectionFactory;

  public SqliteConnectionPolicyInterceptor(SqliteConnectionFactory connectionFactory)
  {
    _connectionFactory = connectionFactory;
  }

  override public void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
  {
    _connectionFactory.ApplyConnectionPolicy(connection);

    base.ConnectionOpened(connection, eventData);
  }

  override public async Task ConnectionOpenedAsync(DbConnection connection,
                                                   ConnectionEndEventData eventData,
                                                   CancellationToken cancellationToken = default)
  {
    await _connectionFactory.ApplyConnectionPolicyAsync(connection, cancellationToken);

    await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
  }
}
