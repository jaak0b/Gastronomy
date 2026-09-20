using Microsoft.Data.Sqlite;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class SqliteConnectionFactoryTest
{
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
    } finally
    {
      SqliteConnection.ClearAllPools();
      File.Delete(path);
    }
  }
}
