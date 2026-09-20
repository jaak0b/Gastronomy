using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests.Persistence;

public sealed class GastronomyAppDbContextTest
{
  [Test]
  public async Task Migrate_OnEmptyDatabase_CreatesEveryTable()
  {
    using SqliteInMemoryFixture fixture = new();

    List<string> tableNames = [];
    using (var command = fixture.Connection.CreateCommand())
    {
      command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE '__EFMigrations%' AND name NOT LIKE 'sqlite_%'";
      using var reader = await command.ExecuteReaderAsync();
      while (await reader.ReadAsync())
      {
        tableNames.Add(reader.GetString(0));
      }
    }

    Assert.That(tableNames,
                Is.EquivalentTo(new[]
                                {
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
                                  "Festivals",
                                  "FestivalStations",
                                  "FestivalCatalogItems"
                                }));
  }
}
