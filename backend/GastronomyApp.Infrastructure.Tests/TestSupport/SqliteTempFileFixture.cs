using GastronomyApp.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class SqliteTempFileFixture : IDisposable
{
  private readonly SqliteConnectionFactory _connectionFactory = new();
  private readonly List<SqliteConnection> _connections = [];
  private readonly List<GastronomyAppDbContext> _contexts = [];

  public SqliteTempFileFixture() : this(TemporaryDatabaseSchema.Migrated)
  {
  }

  public SqliteTempFileFixture(TemporaryDatabaseSchema schema)
  {
    DatabasePath = Path.Combine(Path.GetTempPath(), $"gastronomyapp-test-{Guid.NewGuid():N}.db");

    if (schema == TemporaryDatabaseSchema.Migrated)
    {
      using var migrationContext = CreateContext();
      migrationContext.Database.Migrate();
    }
  }

  public string DatabasePath { get; }

  public void Dispose()
  {
    foreach (var context in _contexts)
      context.Dispose();

    foreach (var connection in _connections)
      connection.Dispose();

    SqliteConnection.ClearAllPools();

    foreach (var path in new[]
                         {
                           DatabasePath,
                           $"{DatabasePath}-wal",
                           $"{DatabasePath}-shm"
                         })
      File.Delete(path);
  }

  public SqliteConnection OpenConnection()
  {
    var connection = _connectionFactory.Open(DatabasePath);
    _connections.Add(connection);

    return connection;
  }

  public GastronomyAppDbContext CreateContext()
  {
    var connection = OpenConnection();

    GastronomyAppDbContext context = new(new DbContextOptionsBuilder<GastronomyAppDbContext>().UseSqlite(connection).Options);
    _contexts.Add(context);

    return context;
  }
}
