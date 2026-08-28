using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class SqliteInMemoryFixture : IDisposable
{

  public SqliteInMemoryFixture()
  {
    Connection = new("Data Source=:memory:");
    Connection.Open();
    DbContext = CreateContext();
    DbContext.Database.Migrate();
  }

  public GastronomyAppDbContext DbContext { get; }

  public SqliteConnection Connection { get; }

  public void Dispose()
  {
    DbContext.Dispose();
    Connection.Dispose();
  }

  public GastronomyAppDbContext CreateContext()
  {
    DbContextOptions<GastronomyAppDbContext> options = new DbContextOptionsBuilder<GastronomyAppDbContext>()
                                                      .UseSqlite(Connection)
                                                      .Options;

    return new(options);
  }
}
