using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class SqliteInMemoryFixture : IDisposable
{
  private readonly SqliteConnection _connection;

  public SqliteInMemoryFixture()
  {
    _connection = new SqliteConnection("Data Source=:memory:");
    _connection.Open();
    DbContext = CreateContext();
    DbContext.Database.Migrate();
  }

  public GastronomyAppDbContext DbContext { get; }

  public SqliteConnection Connection => _connection;

  public GastronomyAppDbContext CreateContext()
  {
    DbContextOptions<GastronomyAppDbContext> options = new DbContextOptionsBuilder<GastronomyAppDbContext>()
        .UseSqlite(_connection)
        .Options;

    return new GastronomyAppDbContext(options);
  }

  public void Dispose()
  {
    DbContext.Dispose();
    _connection.Dispose();
  }
}
