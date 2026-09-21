using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class OpenItemRepositoryTest
{
  private readonly DateTime _orderedAtUtc = new(2026, 8, 27, 18, 30, 0, DateTimeKind.Utc);

  [Test]
  public void FindForSettlementAsync_NullIds_ThrowsArgumentNullException()
  {
    using SqliteInMemoryFixture fixture = new();
    OpenItemRepository repository = new(fixture.DbContext);

    Assert.That(async () => await repository.FindForSettlementAsync(null!, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task FindOpenAtFestivalAsync_ASettledItem_LeavesItOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    IReadOnlyList<Guid> items = await PlaceOrderAsync(fixture, seeded, seeded.FestivalId, "Tisch 12", 1);
    await SettleAsync(fixture, seeded, items[0], 350, _orderedAtUtc.AddMinutes(5));

    OpenItemRepository repository = new(fixture.DbContext);

    IReadOnlyList<OrderItem> open = await repository.FindOpenAtFestivalAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(open.Select(item => item.Id), Is.EqualTo(new[] { items[1] }));
  }

  [Test]
  public async Task FindOpenAtFestivalAsync_AnOrderOfAnotherFestival_LeavesItOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var lastYearFestivalId = await AddFestivalAsync(fixture, "Sommerfest im Vorjahr");
    await PlaceOrderAsync(fixture, seeded, seeded.FestivalId, "Tisch 12", 1);
    await PlaceOrderAsync(fixture, seeded, lastYearFestivalId, "Tisch 1", 1);

    OpenItemRepository repository = new(fixture.DbContext);

    IReadOnlyList<OrderItem> open = await repository.FindOpenAtFestivalAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(open, Has.Count.EqualTo(2));
  }

  [Test]
  public async Task FindOpenAtFestivalAsync_ItemsOfAnOrder_CarryTheOrderThatNamesTheTable()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await PlaceOrderAsync(fixture, seeded, seeded.FestivalId, "Tisch 12", 4);

    OpenItemRepository repository = new(fixture.DbContext);

    IReadOnlyList<OrderItem> open = await repository.FindOpenAtFestivalAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(open, Has.Count.EqualTo(2));
                      Assert.That(open[0].StationOrder.Order.TableName, Is.EqualTo("Tisch 12"));
                      Assert.That(open[0].StationOrder.Order.GlobalOrderNumber, Is.EqualTo(4));
                      Assert.That(open[0].StationOrder.Order.CreatedAtUtc, Is.EqualTo(_orderedAtUtc));
                    });
  }

  [Test]
  public async Task FindForSettlementAsync_ItemsOfAnOrder_CarryTheOrderThatNamesTheTable()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    IReadOnlyList<Guid> items = await PlaceOrderAsync(fixture, seeded, seeded.FestivalId, "Tisch 12", 4);

    OpenItemRepository repository = new(fixture.DbContext);

    IReadOnlyList<OrderItem> selected = await repository.FindForSettlementAsync([items[0]], TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(selected, Has.Count.EqualTo(1));
                      Assert.That(selected[0].StationOrder.Order.TableName, Is.EqualTo("Tisch 12"));
                    });
  }

  [Test]
  public async Task FindTableNamesAtFestivalAsync_SeveralOrdersAtOneTable_NamesThatTableOnce()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await PlaceOrderAsync(fixture, seeded, seeded.FestivalId, "Tisch 12", 1);
    await PlaceOrderAsync(fixture, seeded, seeded.FestivalId, "Tisch 12", 2);
    await PlaceOrderAsync(fixture, seeded, seeded.FestivalId, "Tisch 3", 3);

    OpenItemRepository repository = new(fixture.DbContext);

    IReadOnlyList<string> tableNames = await repository.FindTableNamesAtFestivalAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(tableNames,
                Is.EqualTo(new[]
                           {
                             "Tisch 12",
                             "Tisch 3"
                           }));
  }

  [Test]
  public async Task FindTableOrdersAsync_TwoOrdersOfOneTable_ComeBackNewestFirstWithEveryPositionAndItsState()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    var olderStationOrderId = Guid.NewGuid();
    var olderOrderId = await PlaceOrderWithItemsAsync(fixture, seeded, "T1", 1, _orderedAtUtc, olderStationOrderId, BuildItem(olderStationOrderId, seeded.SausageItemId, "Bratwurst", 350), BuildItem(olderStationOrderId, seeded.SausageItemId, "Limonade", 250));

    var newestStationOrderId = Guid.NewGuid();
    var fulfilledItem = BuildItem(newestStationOrderId, seeded.SausageItemId, "Bratwurst", 350);
    fulfilledItem.FulfilledAtUtc = _orderedAtUtc.AddMinutes(20);
    var settledItem = BuildItem(newestStationOrderId, seeded.SausageItemId, "Limonade", 250);
    settledItem.SettledAtUtc = _orderedAtUtc.AddMinutes(25);
    settledItem.SettledByStaffMemberId = seeded.StaffMemberId;
    settledItem.ChargedPriceCents = 250;
    var newestOrderId = await PlaceOrderWithItemsAsync(fixture, seeded, "T1", 3, _orderedAtUtc.AddMinutes(15), newestStationOrderId, fulfilledItem, settledItem);

    var sameTimeStationOrderId = Guid.NewGuid();
    var sameTimeOrderId = await PlaceOrderWithItemsAsync(fixture, seeded, "T1", 2, _orderedAtUtc.AddMinutes(15), sameTimeStationOrderId, BuildItem(sameTimeStationOrderId, seeded.SausageItemId, "Bier", 300));

    var otherTableStationOrderId = Guid.NewGuid();
    await PlaceOrderWithItemsAsync(fixture, seeded, "Tisch 3", 4, _orderedAtUtc.AddMinutes(30), otherTableStationOrderId, BuildItem(otherTableStationOrderId, seeded.SausageItemId, "Bratwurst", 350));

    OpenItemRepository repository = new(fixture.DbContext);

    IReadOnlyList<Order> orders = await repository.FindTableOrdersAsync(seeded.FestivalId, "T1", TestContext.CurrentContext.CancellationToken);

    IReadOnlyList<OrderItem> newestItems = PositionsOf(orders[0]);

    Assert.Multiple(() =>
                    {
                      Assert.That(orders.Select(order => order.Id),
                                  Is.EqualTo(new[]
                                             {
                                               newestOrderId,
                                               sameTimeOrderId,
                                               olderOrderId
                                             }));
                      Assert.That(orders.Select(order => order.GlobalOrderNumber),
                                  Is.EqualTo(new[]
                                             {
                                               3,
                                               2,
                                               1
                                             }));
                      Assert.That(orders.Select(order => order.StaffMember.Name), Is.All.EqualTo("Anna"));
                      Assert.That(orders[0].CreatedAtUtc, Is.EqualTo(_orderedAtUtc.AddMinutes(15)));
                      Assert.That(newestItems.Select(item => item.Id),
                                  Is.EquivalentTo(new[]
                                                  {
                                                    fulfilledItem.Id,
                                                    settledItem.Id
                                                  }));
                      Assert.That(newestItems.Single(item => item.Id == fulfilledItem.Id).FulfilledAtUtc, Is.EqualTo(_orderedAtUtc.AddMinutes(20)));
                      Assert.That(newestItems.Single(item => item.Id == settledItem.Id).SettledAtUtc, Is.EqualTo(_orderedAtUtc.AddMinutes(25)));
                      Assert.That(newestItems.Single(item => item.Id == settledItem.Id).UnitPriceCents, Is.EqualTo(250));
                      Assert.That(newestItems.Select(item => item.StationOrder.Order.Id), Is.All.EqualTo(newestOrderId));
                      Assert.That(newestItems.Select(item => item.StationOrder.Order.GlobalOrderNumber), Is.All.EqualTo(3));
                    });
  }

  [Test]
  public async Task FindTableOrdersAsync_AUppercaseName_DoesNotMatchTheLowercaseTable()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    var upperStationOrderId = Guid.NewGuid();
    var upperOrderId = await PlaceOrderWithItemsAsync(fixture, seeded, "T1", 1, _orderedAtUtc, upperStationOrderId, BuildItem(upperStationOrderId, seeded.SausageItemId, "Bratwurst", 350));

    var lowerStationOrderId = Guid.NewGuid();
    await PlaceOrderWithItemsAsync(fixture, seeded, "t1", 2, _orderedAtUtc.AddMinutes(5), lowerStationOrderId, BuildItem(lowerStationOrderId, seeded.SausageItemId, "Limonade", 250));

    OpenItemRepository repository = new(fixture.DbContext);

    IReadOnlyList<Order> orders = await repository.FindTableOrdersAsync(seeded.FestivalId, "T1", TestContext.CurrentContext.CancellationToken);

    Assert.That(orders.Select(order => order.Id), Is.EqualTo(new[] { upperOrderId }));
  }

  [Test]
  public void FindTableOrdersAsync_NullTableName_ThrowsArgumentNullException()
  {
    using SqliteInMemoryFixture fixture = new();
    OpenItemRepository repository = new(fixture.DbContext);

    Assert.That(async () => await repository.FindTableOrdersAsync(Guid.NewGuid(), null!, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task FindForSettlementAsync_TheSelectedItems_ReturnsThemSoTheirChangesCanBeSaved()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    IReadOnlyList<Guid> items = await PlaceOrderAsync(fixture, seeded, seeded.FestivalId, "Tisch 12", 1);

    OpenItemRepository repository = new(fixture.DbContext);

    IReadOnlyList<OrderItem> selected = await repository.FindForSettlementAsync([items[0]], TestContext.CurrentContext.CancellationToken);
    selected[0].SettledAtUtc = _orderedAtUtc.AddMinutes(5);
    selected[0].ChargedPriceCents = 350;
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();
    var stored = await readContext.OrderItems.FirstAsync(item => item.Id == items[0], TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(selected, Has.Count.EqualTo(1));
                      Assert.That(stored.ChargedPriceCents, Is.EqualTo(350));
                    });
  }

  private IReadOnlyList<OrderItem> PositionsOf(Order order)
  {
    return order.StationOrders.SelectMany(stationOrder => stationOrder.Items).ToList();
  }

  private async Task<Guid> AddFestivalAsync(SqliteInMemoryFixture fixture, string name)
  {
    var festivalId = Guid.NewGuid();

    fixture.DbContext.Festivals.Add(new()
                                    {
                                      Id = festivalId,
                                      Name = name,
                                      StartsAtUtc = _orderedAtUtc.AddYears(-1),
                                      EndsAtUtc = _orderedAtUtc.AddYears(-1).AddDays(2),
                                      NextOrderNumber = 1,
                                      IsHidden = false
                                    });

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return festivalId;
  }

  private async Task<IReadOnlyList<Guid>> PlaceOrderAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, Guid festivalId, string tableName, int globalOrderNumber)
  {
    var orderId = Guid.NewGuid();
    var stationOrderId = Guid.NewGuid();

    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = festivalId,
                    GlobalOrderNumber = globalOrderNumber,
                    StaffMemberId = seeded.StaffMemberId,
                    TableName = tableName,
                    CreatedAtUtc = _orderedAtUtc
                  };

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = orderId,
                                  FestivalId = festivalId,
                                  StationId = seeded.KitchenStationId,
                                  StationOrderNumber = globalOrderNumber,
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
             UnitPriceCents = unitPriceCents,
             Note = null
           };
  }

  private async Task<Guid> PlaceOrderWithItemsAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, string tableName, int globalOrderNumber, DateTime createdAtUtc, Guid stationOrderId, params OrderItem[] items)
  {
    var orderId = Guid.NewGuid();

    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = seeded.FestivalId,
                    GlobalOrderNumber = globalOrderNumber,
                    StaffMemberId = seeded.StaffMemberId,
                    TableName = tableName,
                    CreatedAtUtc = createdAtUtc
                  };

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = orderId,
                                  FestivalId = seeded.FestivalId,
                                  StationId = seeded.KitchenStationId,
                                  StationOrderNumber = globalOrderNumber,
                                  DeliveryMode = DeliveryMode.Together
                                };

    order.StationOrders.Add(stationOrder);

    foreach (var item in items)
      stationOrder.Items.Add(item);

    fixture.DbContext.Orders.Add(order);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return orderId;
  }

  private async Task SettleAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, Guid orderItemId, int chargedPriceCents, DateTime settledAtUtc)
  {
    var item = await fixture.DbContext.OrderItems.FirstAsync(candidate => candidate.Id == orderItemId, TestContext.CurrentContext.CancellationToken);

    item.SettledAtUtc = settledAtUtc;
    item.SettledByStaffMemberId = seeded.StaffMemberId;
    item.ChargedPriceCents = chargedPriceCents;

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
