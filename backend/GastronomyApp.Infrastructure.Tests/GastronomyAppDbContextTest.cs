using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class GastronomyAppDbContextTest
{
  private readonly static string[] ExpectedTableNames =
  [
    "Stations",
    "CatalogCategories",
    "CatalogItems",
    "ItemStationAssignments",
    "StaffMembers",
    "Devices",
    "EnrolmentInvitations",
    "Orders",
    "StationOrders",
    "OrderItems",
    "OrderItemStatusChanges",
    "Festivals",
    "FestivalStations",
    "FestivalCatalogItems"
  ];

  [Test]
  public async Task Migrate_OnEmptyDatabase_CreatesEveryTable()
  {
    using SqliteInMemoryFixture fixture = new();

    List<string> tableNames = [];
    using (var command = fixture.Connection.CreateCommand())
    {
      command.CommandText =
        "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE '__EFMigrations%' AND name NOT LIKE 'sqlite_%'";
      using var reader = await command.ExecuteReaderAsync();
      while (await reader.ReadAsync())
      {
        tableNames.Add(reader.GetString(0));
      }
    }

    Assert.That(tableNames, Is.EquivalentTo(ExpectedTableNames));
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
    } finally
    {
      SqliteConnection.ClearAllPools();
      File.Delete(path);
    }
  }
}
