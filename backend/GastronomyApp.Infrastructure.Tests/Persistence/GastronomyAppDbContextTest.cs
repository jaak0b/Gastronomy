using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

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

  [Test]
  public async Task Include_OnOrderItem_LoadsStationOrderCatalogItemAndSettlingStaffMember()
  {
    using SqliteInMemoryFixture fixture = new();
    DomainSeeder seeder = new();
    var seeded = await seeder.SeedAsync(fixture.DbContext, CancellationToken.None);
    var orderItemId = Guid.NewGuid();
    DateTime settledAtUtc = new(2026, 8, 27, 19, 0, 0, DateTimeKind.Utc);

    Order order = new()
                  {
                    Id = Guid.NewGuid(),
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = seeded.FestivalId,
                    GlobalOrderNumber = 1,
                    StaffMemberId = seeded.StaffMemberId,
                    TableName = "Tisch 3",
                    CreatedAtUtc = settledAtUtc
                  };
    StationOrder stationOrder = new()
                                {
                                  Id = Guid.NewGuid(),
                                  OrderId = order.Id,
                                  FestivalId = seeded.FestivalId,
                                  StationId = seeded.KitchenStationId,
                                  StationOrderNumber = 1,
                                  DeliveryMode = DeliveryMode.Together
                                };
    stationOrder.Items.Add(new()
                           {
                             Id = orderItemId,
                             StationOrderId = stationOrder.Id,
                             CatalogItemId = seeded.SausageItemId,
                             ItemName = "Bratwurst",
                             UnitPriceCents = 350,
                             SettledAtUtc = settledAtUtc,
                             ChargedPriceCents = 350,
                             SettledByStaffMemberId = seeded.StaffMemberId
                           });
    order.StationOrders.Add(stationOrder);
    fixture.DbContext.Orders.Add(order);
    await fixture.DbContext.SaveChangesAsync();

    using var readingContext = fixture.CreateContext();
    var loaded = await readingContext.OrderItems.Include(item => item.StationOrder)
                                     .ThenInclude(loadedStationOrder => loadedStationOrder.Station)
                                     .Include(item => item.CatalogItem)
                                     .ThenInclude(catalogItem => catalogItem.Category)
                                     .Include(item => item.SettledByStaffMember)
                                     .SingleAsync(item => item.Id == orderItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(loaded.StationOrder.StationOrderNumber, Is.EqualTo(1));
                      Assert.That(loaded.StationOrder.Station.Name, Is.EqualTo("Kueche"));
                      Assert.That(loaded.CatalogItem.Name, Is.EqualTo("Bratwurst"));
                      Assert.That(loaded.CatalogItem.Category.Name, Is.EqualTo("Speisen"));
                      Assert.That(loaded.SettledByStaffMember!.Name, Is.EqualTo("Anna"));
                    });
  }

  [Test]
  public async Task Include_OnFestival_LoadsStationsAndMenu()
  {
    using SqliteInMemoryFixture fixture = new();
    DomainSeeder seeder = new();
    var seeded = await seeder.SeedAsync(fixture.DbContext, CancellationToken.None);

    using var readingContext = fixture.CreateContext();
    var festival = await readingContext.Festivals.Include(loaded => loaded.Stations)
                                       .ThenInclude(link => link.Station)
                                       .Include(loaded => loaded.CatalogItems)
                                       .ThenInclude(menuRow => menuRow.CatalogItem)
                                       .Include(loaded => loaded.ItemStationAssignments)
                                       .SingleAsync(loaded => loaded.Id == seeded.FestivalId);

    Assert.Multiple(() =>
                    {
                      Assert.That(festival.Stations.Select(link => link.Station.Name),
                                  Is.EquivalentTo(new[]
                                                  {
                                                    "Kueche",
                                                    "Theke"
                                                  }));
                      Assert.That(festival.CatalogItems.Select(menuRow => menuRow.CatalogItem.Name),
                                  Is.EquivalentTo(new[]
                                                  {
                                                    "Bratwurst",
                                                    "Limonade"
                                                  }));
                      Assert.That(festival.ItemStationAssignments, Has.Count.EqualTo(2));
                    });
  }
}
