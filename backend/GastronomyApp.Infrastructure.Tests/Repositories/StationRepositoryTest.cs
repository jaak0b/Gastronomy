using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class StationRepositoryTest
{
  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

  [Test]
  public async Task FindAtFestivalAsync_TheStationsOfOneFestival_ReturnsThemByTheirPlaceInTheList()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext);

    IReadOnlyCollection<Station> stations = await repository.FindAtFestivalAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stations.Select(station => station.Id),
                Is.EqualTo(new[]
                           {
                             seeded.KitchenStationId,
                             seeded.BarStationId
                           }));
  }

  [Test]
  public async Task FindStationIdsWithItemsToHandOutAsync_AStationWithAnItemNotYetHandedOutAtTheFestival_NamesThatStationOnly()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await PlaceKitchenOrderAsync(fixture, seeded, seeded.FestivalId);
    StationRepository repository = new(fixture.DbContext);

    IReadOnlyCollection<Guid> stationIds = await repository.FindStationIdsWithItemsToHandOutAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stationIds, Is.EqualTo(new[] { seeded.KitchenStationId }));
  }

  [Test]
  public async Task FindStationIdsWithItemsToHandOutAsync_AStationThatHandedOutEverything_NamesNoStation()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await HandOutAsync(fixture, await PlaceKitchenOrderAsync(fixture, seeded, seeded.FestivalId));
    StationRepository repository = new(fixture.DbContext);

    IReadOnlyCollection<Guid> stationIds = await repository.FindStationIdsWithItemsToHandOutAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stationIds, Is.Empty);
  }

  [Test]
  public async Task FindStationIdsWithItemsToHandOutAsync_AnItemNotYetHandedOutAtAnotherFestival_NamesNoStation()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var autumnFestivalId = await SeedAutumnFestivalAsync(fixture);
    await PlaceKitchenOrderAsync(fixture, seeded, autumnFestivalId);
    StationRepository repository = new(fixture.DbContext);

    IReadOnlyCollection<Guid> stationIds = await repository.FindStationIdsWithItemsToHandOutAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stationIds, Is.Empty);
  }

  [Test]
  public async Task ExistsAsync_AStationNobodyEverCreated_AnswersFalse()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext);

    Assert.Multiple(async () =>
                    {
                      Assert.That(await repository.ExistsAsync(seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken), Is.True);
                      Assert.That(await repository.ExistsAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken), Is.False);
                    });
  }

  [Test]
  public async Task FindAtFestivalWithOpenItemsAsync_TheStationsOfOneFestival_ReturnsThemByTheirPlaceInTheList()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext);

    IReadOnlyList<Station> stations = await repository.FindAtFestivalWithOpenItemsAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stations.Select(station => station.Id),
                Is.EqualTo(new[]
                           {
                             seeded.KitchenStationId,
                             seeded.BarStationId
                           }));
  }

  [Test]
  public async Task FindAtFestivalWithOpenItemsAsync_OpenItems_CarryTheArticleTheyWereOrderedFrom()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await GiveTheSausageAProductionTimeAsync(fixture, seeded);
    await PlaceKitchenOrderAsync(fixture, seeded, seeded.FestivalId);

    StationRepository repository = new(fixture.DbContext);

    IReadOnlyList<Station> stations = await repository.FindAtFestivalWithOpenItemsAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stations[0].StationOrders.SelectMany(stationOrder => stationOrder.Items).Select(item => item.CatalogItem.ProductionMinutes),
                Is.EquivalentTo(new double?[]
                                {
                                  4,
                                  null
                                }));
  }

  [Test]
  public async Task FindAtFestivalWithOpenItemsAsync_AnItemTheStationHandedOut_LeavesItOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    IReadOnlyList<Guid> items = await PlaceKitchenOrderAsync(fixture, seeded, seeded.FestivalId);
    await HandOutAsync(fixture, items);

    StationRepository repository = new(fixture.DbContext);

    IReadOnlyList<Station> stations = await repository.FindAtFestivalWithOpenItemsAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stations.SelectMany(station => station.StationOrders).SelectMany(stationOrder => stationOrder.Items), Is.Empty);
  }

  private async Task GiveTheSausageAProductionTimeAsync(SqliteInMemoryFixture fixture, SeededDomain seeded)
  {
    var sausage = await fixture.DbContext.CatalogItems.FirstAsync(item => item.Id == seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);
    sausage.ProductionMinutes = 4;

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<Guid> SeedAutumnFestivalAsync(SqliteInMemoryFixture fixture)
  {
    var festivalId = Guid.NewGuid();

    fixture.DbContext.Festivals.Add(new()
                                    {
                                      Id = festivalId,
                                      Name = "Herbstfest",
                                      StartsAtUtc = _now.AddDays(-60),
                                      EndsAtUtc = _now.AddDays(-59),
                                      NextOrderNumber = 1,
                                      IsHidden = false
                                    });
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return festivalId;
  }

  private async Task HandOutAsync(SqliteInMemoryFixture fixture, IReadOnlyCollection<Guid> orderItemIds)
  {
    List<Guid> ids = orderItemIds.ToList();
    List<OrderItem> items = await fixture.DbContext.OrderItems.Where(item => ids.Contains(item.Id)).ToListAsync(TestContext.CurrentContext.CancellationToken);

    foreach (var item in items)
      item.FulfilledAtUtc = _now.AddMinutes(5);

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<IReadOnlyList<Guid>> PlaceKitchenOrderAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, Guid festivalId)
  {
    var tableId = await new DomainSeeder().SeedTableAsync(fixture.DbContext, festivalId, "Tisch 12", TestContext.CurrentContext.CancellationToken);
    var orderId = Guid.NewGuid();
    var stationOrderId = Guid.NewGuid();

    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = festivalId,
                    GlobalOrderNumber = 1,
                    StaffMemberId = seeded.StaffMemberId,
                    TableId = tableId,
                    CreatedAtUtc = _now
                  };

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = orderId,
                                  FestivalId = festivalId,
                                  StationId = seeded.KitchenStationId,
                                  StationOrderNumber = 1,
                                  DeliveryMode = DeliveryMode.Together
                                };

    stationOrder.Items.Add(BuildItem(stationOrderId, seeded.SausageItemId, "Bratwurst", 350));
    stationOrder.Items.Add(BuildItem(stationOrderId, seeded.LemonadeItemId, "Limonade", 250));
    order.StationOrders.Add(stationOrder);

    fixture.DbContext.Orders.Add(order);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return stationOrder.Items.Select(item => item.Id).ToList();
  }

  private OrderItem BuildItem(Guid stationOrderId, Guid catalogItemId, string itemName, int unitPriceCents)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = stationOrderId,
             CatalogItemId = catalogItemId,
             ItemName = itemName,
             UnitPriceCents = unitPriceCents
           };
  }
}
