using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.ReadModels;
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
    OpenItemRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    Assert.That(async () => await repository.FindForSettlementAsync(null!, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public void FindOwnersAsync_NullIds_ThrowsArgumentNullException()
  {
    using SqliteInMemoryFixture fixture = new();
    OpenItemRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    Assert.That(async () => await repository.FindOwnersAsync(null!, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task FindOpenAtFestivalAsync_ASettledItem_LeavesItOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    IReadOnlyList<Guid> items = await PlaceOrderAsync(fixture, seeded.FestivalId, seeded.KitchenStationId, "Tisch 12", 1);
    await SettleAsync(fixture, items[0], 350, _orderedAtUtc.AddMinutes(5));

    OpenItemRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<OrderItem> open = await repository.FindOpenAtFestivalAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(open.Select(item => item.Id), Is.EqualTo(new[] { items[1] }));
  }

  [Test]
  public async Task FindOpenAtFestivalAsync_AnOrderOfAnotherFestival_LeavesItOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var lastYearFestivalId = await AddFestivalAsync(fixture, "Sommerfest im Vorjahr");
    await PlaceOrderAsync(fixture, seeded.FestivalId, seeded.KitchenStationId, "Tisch 12", 1);
    await PlaceOrderAsync(fixture, lastYearFestivalId, seeded.KitchenStationId, "Tisch 1", 1);

    OpenItemRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<OrderItem> open = await repository.FindOpenAtFestivalAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(open, Has.Count.EqualTo(2));
  }

  [Test]
  public async Task FindOwnersAsync_ItemsOfAnOrder_NamesTheTableAndTheOrderNumber()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    IReadOnlyList<Guid> items = await PlaceOrderAsync(fixture, seeded.FestivalId, seeded.KitchenStationId, "Tisch 12", 4);

    OpenItemRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyDictionary<Guid, OrderItemOwner> owners = await repository.FindOwnersAsync(items, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(owners, Has.Count.EqualTo(2));
                      Assert.That(owners[items[0]].TableName, Is.EqualTo("Tisch 12"));
                      Assert.That(owners[items[0]].GlobalOrderNumber, Is.EqualTo(4));
                      Assert.That(owners[items[0]].OrderedAtUtc, Is.EqualTo(_orderedAtUtc));
                    });
  }

  [Test]
  public async Task FindTableNamesAtFestivalAsync_SeveralOrdersAtOneTable_NamesThatTableOnce()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await PlaceOrderAsync(fixture, seeded.FestivalId, seeded.KitchenStationId, "Tisch 12", 1);
    await PlaceOrderAsync(fixture, seeded.FestivalId, seeded.KitchenStationId, "Tisch 12", 2);
    await PlaceOrderAsync(fixture, seeded.FestivalId, seeded.KitchenStationId, "Tisch 3", 3);

    OpenItemRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

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
    var olderOrderId = await PlaceOrderWithItemsAsync(fixture, seeded, "T1", 1, _orderedAtUtc, olderStationOrderId, BuildItem(olderStationOrderId, "Bratwurst", 350), BuildItem(olderStationOrderId, "Limonade", 250));

    var newestStationOrderId = Guid.NewGuid();
    var fulfilledItem = BuildItem(newestStationOrderId, "Bratwurst", 350);
    fulfilledItem.FulfilledAtUtc = _orderedAtUtc.AddMinutes(20);
    var settledItem = BuildItem(newestStationOrderId, "Limonade", 250);
    settledItem.SettledAtUtc = _orderedAtUtc.AddMinutes(25);
    settledItem.SettledByStaffMemberId = seeded.StaffMemberId;
    settledItem.ChargedPriceCents = 250;
    var newestOrderId = await PlaceOrderWithItemsAsync(fixture, seeded, "T1", 3, _orderedAtUtc.AddMinutes(15), newestStationOrderId, fulfilledItem, settledItem);

    var sameTimeStationOrderId = Guid.NewGuid();
    var sameTimeOrderId = await PlaceOrderWithItemsAsync(fixture, seeded, "T1", 2, _orderedAtUtc.AddMinutes(15), sameTimeStationOrderId, BuildItem(sameTimeStationOrderId, "Bier", 300));

    var otherTableStationOrderId = Guid.NewGuid();
    await PlaceOrderWithItemsAsync(fixture, seeded, "Tisch 3", 4, _orderedAtUtc.AddMinutes(30), otherTableStationOrderId, BuildItem(otherTableStationOrderId, "Bratwurst", 350));

    OpenItemRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<TableOrderRecord> orders = await repository.FindTableOrdersAsync(seeded.FestivalId, "T1", TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(orders.Select(order => order.OrderId),
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
                      Assert.That(orders.Select(order => order.StaffMemberName), Is.All.EqualTo("Anna"));
                      Assert.That(orders[0].CreatedAtUtc, Is.EqualTo(_orderedAtUtc.AddMinutes(15)));
                      Assert.That(orders[0].Items.Select(item => item.OrderItemId),
                                  Is.EqualTo(new[]
                                             {
                                               fulfilledItem.Id,
                                               settledItem.Id
                                             }));
                      Assert.That(orders[0].Items.Select(item => item.ItemName),
                                  Is.EqualTo(new[]
                                             {
                                               "Bratwurst",
                                               "Limonade"
                                             }));
                      Assert.That(orders[0].Items.Select(item => item.UnitPriceCents),
                                  Is.EqualTo(new[]
                                             {
                                               350,
                                               250
                                             }));
                      Assert.That(orders[0].Items.Select(item => item.FulfilledAtUtc),
                                  Is.EqualTo(new DateTime?[]
                                             {
                                               _orderedAtUtc.AddMinutes(20),
                                               null
                                             }));
                      Assert.That(orders[0].Items.Select(item => item.SettledAtUtc),
                                  Is.EqualTo(new DateTime?[]
                                             {
                                               null,
                                               _orderedAtUtc.AddMinutes(25)
                                             }));
                      Assert.That(orders[0].Items.Select(item => item.OrderId), Is.All.EqualTo(newestOrderId));
                      Assert.That(orders[0].Items.Select(item => item.GlobalOrderNumber), Is.All.EqualTo(3));
                    });
  }

  [Test]
  public async Task FindTableOrdersAsync_AUppercaseName_DoesNotMatchTheLowercaseTable()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    var upperStationOrderId = Guid.NewGuid();
    var upperOrderId = await PlaceOrderWithItemsAsync(fixture, seeded, "T1", 1, _orderedAtUtc, upperStationOrderId, BuildItem(upperStationOrderId, "Bratwurst", 350));

    var lowerStationOrderId = Guid.NewGuid();
    await PlaceOrderWithItemsAsync(fixture, seeded, "t1", 2, _orderedAtUtc.AddMinutes(5), lowerStationOrderId, BuildItem(lowerStationOrderId, "Limonade", 250));

    OpenItemRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<TableOrderRecord> orders = await repository.FindTableOrdersAsync(seeded.FestivalId, "T1", TestContext.CurrentContext.CancellationToken);

    Assert.That(orders.Select(order => order.OrderId), Is.EqualTo(new[] { upperOrderId }));
  }

  [Test]
  public async Task FindTableOrdersAsync_ThePositionsOfAnOrder_ComeBackByNameThenNoteThenId()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    var stationOrderId = Guid.NewGuid();
    var schnitzel = BuildItem(stationOrderId, "Schnitzel", 400);
    schnitzel.Id = new("00000000-0000-0000-0000-000000000003");
    var notedBratwurst = BuildItem(stationOrderId, "Bratwurst", 350);
    notedBratwurst.Id = new("00000000-0000-0000-0000-000000000001");
    notedBratwurst.Note = "Ohne Ketchup";
    var plainBratwurst = BuildItem(stationOrderId, "Bratwurst", 350);
    plainBratwurst.Id = new("00000000-0000-0000-0000-000000000002");

    await PlaceOrderWithItemsAsync(fixture, seeded, "Tisch 12", 1, _orderedAtUtc, stationOrderId, schnitzel, notedBratwurst, plainBratwurst);

    OpenItemRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<TableOrderRecord> orders = await repository.FindTableOrdersAsync(seeded.FestivalId, "Tisch 12", TestContext.CurrentContext.CancellationToken);

    Assert.That(orders[0].Items.Select(item => item.OrderItemId),
                Is.EqualTo(new[]
                           {
                             plainBratwurst.Id,
                             notedBratwurst.Id,
                             schnitzel.Id
                           }));
  }

  [Test]
  public void FindTableOrdersAsync_NullTableName_ThrowsArgumentNullException()
  {
    using SqliteInMemoryFixture fixture = new();
    OpenItemRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    Assert.That(async () => await repository.FindTableOrdersAsync(Guid.NewGuid(), null!, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task FindForSettlementAsync_TheSelectedItems_ReturnsThemSoTheirChangesCanBeSaved()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    IReadOnlyList<Guid> items = await PlaceOrderAsync(fixture, seeded.FestivalId, seeded.KitchenStationId, "Tisch 12", 1);

    OpenItemRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

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

  private async Task<IReadOnlyList<Guid>> PlaceOrderAsync(SqliteInMemoryFixture fixture, Guid festivalId, Guid stationId, string tableName, int globalOrderNumber)
  {
    var orderId = Guid.NewGuid();
    var stationOrderId = Guid.NewGuid();

    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = festivalId,
                    GlobalOrderNumber = globalOrderNumber,
                    StaffMemberId = Guid.NewGuid(),
                    TableName = tableName,
                    CreatedAtUtc = _orderedAtUtc
                  };

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = orderId,
                                  FestivalId = festivalId,
                                  StationId = stationId,
                                  StationOrderNumber = globalOrderNumber,
                                  DeliveryMode = DeliveryMode.Together
                                };

    stationOrder.Items.Add(BuildItem(stationOrderId, "Bratwurst", 350));
    stationOrder.Items.Add(BuildItem(stationOrderId, "Limonade", 250));
    order.StationOrders.Add(stationOrder);

    fixture.DbContext.Orders.Add(order);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return stationOrder.Items.Select(item => item.Id).ToList();
  }

  private OrderItem BuildItem(Guid stationOrderId, string itemName, int unitPriceCents)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = stationOrderId,
             CatalogItemId = Guid.NewGuid(),
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

  private async Task SettleAsync(SqliteInMemoryFixture fixture, Guid orderItemId, int chargedPriceCents, DateTime settledAtUtc)
  {
    var item = await fixture.DbContext.OrderItems.FirstAsync(candidate => candidate.Id == orderItemId, TestContext.CurrentContext.CancellationToken);

    item.SettledAtUtc = settledAtUtc;
    item.SettledByStaffMemberId = Guid.NewGuid();
    item.ChargedPriceCents = chargedPriceCents;

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
