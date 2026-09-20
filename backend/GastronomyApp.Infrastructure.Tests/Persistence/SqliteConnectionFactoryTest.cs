using GastronomyApp.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace GastronomyApp.Infrastructure.Tests.Persistence;

public sealed class SqliteConnectionFactoryTest
{
  [Test]
  public void Open_NullDataSource_ThrowsArgumentNullException()
  {
    SqliteConnectionFactory factory = new();

    Assert.That(() => factory.Open(null!), Throws.ArgumentNullException);
  }

  [Test]
  public void Open_EmptyDataSource_ThrowsArgumentException()
  {
    SqliteConnectionFactory factory = new();

    Assert.That(() => factory.Open(""), Throws.ArgumentException);
  }

  [Test]
  public void ApplyConnectionPolicy_NullConnection_ThrowsArgumentNullException()
  {
    SqliteConnectionFactory factory = new();

    Assert.That(() => factory.ApplyConnectionPolicy(null!), Throws.ArgumentNullException);
  }

  [Test]
  public void ApplyConnectionPolicyAsync_NullConnection_ThrowsArgumentNullException()
  {
    SqliteConnectionFactory factory = new();

    Assert.That(() => factory.ApplyConnectionPolicyAsync(null!, CancellationToken.None), Throws.ArgumentNullException);
  }

  [Test]
  public async Task OpenConnection_OnFile_SetsWalModeAndBusyTimeout()
  {
    var path = Path.Combine(Path.GetTempPath(), $"gastronomyapp-test-{Guid.NewGuid():N}.db");
    SqliteConnectionFactory factory = new();

    try
    {
      await using var connection = factory.Open(path);

      using var journalMode = connection.CreateCommand();
      journalMode.CommandText = "PRAGMA journal_mode";
      var journalModeValue = await journalMode.ExecuteScalarAsync();

      using var busyTimeout = connection.CreateCommand();
      busyTimeout.CommandText = "PRAGMA busy_timeout";
      var busyTimeoutValue = await busyTimeout.ExecuteScalarAsync();

      Assert.Multiple(() =>
                      {
                        Assert.That(journalModeValue, Is.EqualTo("wal"));
                        Assert.That(Convert.ToInt32(busyTimeoutValue), Is.EqualTo(5000));
                      });
    }
    finally
    {
      SqliteConnection.ClearAllPools();
      File.Delete(path);
    }
  }
}
