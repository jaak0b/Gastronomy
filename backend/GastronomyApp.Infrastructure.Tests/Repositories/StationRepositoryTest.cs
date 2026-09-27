using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class StationRepositoryTest
{
  private readonly StationService _stationService = new();
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
  public async Task FindAllAsync_WithoutAFestival_SaysNoStationBelongsToOne()
  {
    using SqliteInMemoryFixture fixture = new();
    await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext);

    IReadOnlyList<Station> stations = await repository.FindAllAsync(null, TestContext.CurrentContext.CancellationToken);

    Assert.That(stations.Select(station => _stationService.IsAtAnyFestival(station)), Is.All.False);
  }

  [Test]
  public async Task FindAllAsync_AFestivalTheStationTakesPartIn_SaysTheStationBelongsToIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext);

    IReadOnlyList<Station> stations = await repository.FindAllAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stations.Select(station => _stationService.IsAtAnyFestival(station)), Is.All.True);
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
  public async Task AddAsync_ANewStation_StoresItWhenTheChangesAreSaved()
  {
    using SqliteInMemoryFixture fixture = new();
    await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext);
    var stationId = Guid.NewGuid();

    await repository.AddAsync(new()
                              {
                                Id = stationId,
                                Name = "Kuchenbuffet",
                                SortOrder = 3,
                                IsActive = true
                              },
                              TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();

    Assert.That(await readContext.Stations.AnyAsync(station => station.Id == stationId, TestContext.CurrentContext.CancellationToken), Is.True);
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
    await PlaceKitchenOrderAsync(fixture, seeded);

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
    IReadOnlyList<Guid> items = await PlaceKitchenOrderAsync(fixture, seeded);
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

  private async Task HandOutAsync(SqliteInMemoryFixture fixture, IReadOnlyCollection<Guid> orderItemIds)
  {
    List<Guid> ids = orderItemIds.ToList();
    List<OrderItem> items = await fixture.DbContext.OrderItems.Where(item => ids.Contains(item.Id)).ToListAsync(TestContext.CurrentContext.CancellationToken);

    foreach (var item in items)
      item.FulfilledAtUtc = _now.AddMinutes(5);

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<IReadOnlyList<Guid>> PlaceKitchenOrderAsync(SqliteInMemoryFixture fixture, SeededDomain seeded)
  {
    var orderId = Guid.NewGuid();
    var stationOrderId = Guid.NewGuid();

    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = seeded.FestivalId,
                    GlobalOrderNumber = 1,
                    StaffMemberId = seeded.StaffMemberId,
                    TableName = "Tisch 12",
                    CreatedAtUtc = _now
                  };

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = orderId,
                                  FestivalId = seeded.FestivalId,
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
