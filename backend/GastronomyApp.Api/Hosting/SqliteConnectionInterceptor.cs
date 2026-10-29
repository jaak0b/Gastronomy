using System.Data.Common;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Hosting;

public sealed class SqliteConnectionInterceptor : DbConnectionInterceptor
{
  private readonly SqliteConnectionFactory _connectionFactory;
  private readonly ILogger<SqliteConnectionInterceptor> _logger;

  public SqliteConnectionInterceptor(SqliteConnectionFactory connectionFactory, ILogger<SqliteConnectionInterceptor> logger)
  {
    _connectionFactory = connectionFactory;
    _logger = logger;
  }

  override public void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
  {
    _connectionFactory.ApplyConnectionPolicy(connection);

    base.ConnectionOpened(connection, eventData);
  }

  override public async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
  {
    await _connectionFactory.ApplyConnectionPolicyAsync(connection, cancellationToken);

    await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
  }

  override public void ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData)
  {
    ArgumentNullException.ThrowIfNull(connection);
    ArgumentNullException.ThrowIfNull(eventData);

    LogFailure(connection, eventData.Exception);

    base.ConnectionFailed(connection, eventData);
  }

  override public Task ConnectionFailedAsync(DbConnection connection, ConnectionErrorEventData eventData, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(connection);
    ArgumentNullException.ThrowIfNull(eventData);

    if (eventData.Exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
      _logger.LogDebug(eventData.Exception, "The caller cancelled its use of the database connection to {DataSource}.", connection.DataSource);
    else
      LogFailure(connection, eventData.Exception);

    return base.ConnectionFailedAsync(connection, eventData, cancellationToken);
  }

  private void LogFailure(DbConnection connection, Exception exception)
  {
    _logger.LogError(exception, "The database connection to {DataSource} failed: {Reason}", connection.DataSource, exception.Message);
  }
}
