using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class SqliteTempFileFixture : IDisposable
{
  private readonly SqliteConnectionFactory _connectionFactory = new();
  private readonly List<SqliteConnection> _connections = [];
  private readonly List<GastronomyAppDbContext> _contexts = [];

  public SqliteTempFileFixture()
  {
    DatabasePath = Path.Combine(Path.GetTempPath(), $"gastronomyapp-test-{Guid.NewGuid():N}.db");

    using GastronomyAppDbContext migrationContext = CreateContext();
    migrationContext.Database.Migrate();
  }

  public string DatabasePath { get; }

  public GastronomyAppDbContext CreateContext()
  {
    SqliteConnection connection = _connectionFactory.Open(DatabasePath);
    _connections.Add(connection);

    GastronomyAppDbContext context = new(new DbContextOptionsBuilder<GastronomyAppDbContext>()
        .UseSqlite(connection)
        .Options);
    _contexts.Add(context);

    return context;
  }

  public void Dispose()
  {
    foreach (GastronomyAppDbContext context in _contexts)
    {
      context.Dispose();
    }

    foreach (SqliteConnection connection in _connections)
    {
      connection.Dispose();
    }

    SqliteConnection.ClearAllPools();

    foreach (string path in new[] { DatabasePath, $"{DatabasePath}-wal", $"{DatabasePath}-shm" })
    {
      File.Delete(path);
    }
  }
}
