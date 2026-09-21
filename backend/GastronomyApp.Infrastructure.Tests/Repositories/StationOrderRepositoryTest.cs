using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class StationOrderRepositoryTest
{
  private readonly DateTime _orderedAtUtc = new(2026, 8, 27, 18, 30, 0, DateTimeKind.Utc);

  [Test]
  public void FindItemsAtStationAsync_NullIds_ThrowsArgumentNullException()
  {
    using SqliteInMemoryFixture fixture = new();
    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    Assert.That(async () => await repository.FindItemsAtStationAsync(null!, Guid.NewGuid(), TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public void FindOrderIdsOfStationOrdersAsync_NullIds_ThrowsArgumentNullException()
  {
    using SqliteInMemoryFixture fixture = new();
    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    Assert.That(async () => await repository.FindOrderIdsOfStationOrdersAsync(null!, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public void FindOrdersWithItemsAsync_NullIds_ThrowsArgumentNullException()
  {
    using SqliteInMemoryFixture fixture = new();
    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    Assert.That(async () => await repository.FindOrdersWithItemsAsync(null!, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task FindUnfinishedAtStationAsync_AStationOrderWithOpenItems_CarriesTheTableAndEveryLine()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await PlaceOrderAsync(fixture, seeded, seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.AsItComes);

    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<QueuedStationOrder> queue = await repository.FindUnfinishedAtStationAsync(seeded.FestivalId, seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queue, Has.Count.EqualTo(1));
                      Assert.That(queue[0].TableName, Is.EqualTo("Tisch 12"));
                      Assert.That(queue[0].StaffMemberName, Is.EqualTo("Anna"));
                      Assert.That(queue[0].GlobalOrderNumber, Is.EqualTo(1));
                      Assert.That(queue[0].DeliveryMode, Is.EqualTo(DeliveryMode.AsItComes));
                      Assert.That(queue[0].ItemCount, Is.EqualTo(2));
                      Assert.That(queue[0].FulfilledItemCount, Is.Zero);
                      Assert.That(queue[0].Items.Select(item => item.ItemName),
                                  Is.EquivalentTo(new[]
                                                  {
                                                    "Bratwurst",
                                                    "Limonade"
                                                  }));
                    });
  }

  [Test]
  public async Task FindUnfinishedAtStationAsync_AnItemNoteTheWaiterTyped_TravelsWithTheLine()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await PlaceOrderAsync(fixture, seeded, seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);

    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<QueuedStationOrder> queue = await repository.FindUnfinishedAtStationAsync(seeded.FestivalId, seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(queue[0].Items.Select(item => item.Note), Contains.Item("Ohne Ketchup"));
  }

  [Test]
  public async Task FindUnfinishedAtStationAsync_AnItemAnotherWasOrderedBetween_ComeBackTogetherByNameAndNote()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    var stationOrderId = Guid.NewGuid();
    var repository = await AnOrderWithItemsAsync(fixture,
                                                 seeded,
                                                 stationOrderId,
                                                 BuildItemWithId(stationOrderId, seeded.SausageItemId, "Käsekrainer", "00000000-0000-0000-0000-000000000002", null),
                                                 BuildItemWithId(stationOrderId, seeded.LemonadeItemId, "Schnitzel", "00000000-0000-0000-0000-000000000001", null),
                                                 BuildItemWithId(stationOrderId, seeded.SausageItemId, "Käsekrainer", "00000000-0000-0000-0000-000000000003", null));

    IReadOnlyList<QueuedStationOrder> queue = await repository.FindUnfinishedAtStationAsync(seeded.FestivalId, seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(queue[0].Items.Select(item => item.ItemName),
                Is.EqualTo(new[]
                           {
                             "Käsekrainer",
                             "Käsekrainer",
                             "Schnitzel"
                           }));
  }

  [Test]
  public async Task FindUnfinishedAtStationAsync_TwoOfTheSameItemWithDifferentNotes_KeepTheNoteOnItsOwnLine()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    var stationOrderId = Guid.NewGuid();
    var repository = await AnOrderWithItemsAsync(fixture,
                                                 seeded,
                                                 stationOrderId,
                                                 BuildItemWithId(stationOrderId, seeded.SausageItemId, "Käsekrainer", "00000000-0000-0000-0000-000000000002", "Ohne Ketchup"),
                                                 BuildItemWithId(stationOrderId, seeded.LemonadeItemId, "Schnitzel", "00000000-0000-0000-0000-000000000001", null),
                                                 BuildItemWithId(stationOrderId, seeded.SausageItemId, "Käsekrainer", "00000000-0000-0000-0000-000000000003", null));

    IReadOnlyList<QueuedStationOrder> queue = await repository.FindUnfinishedAtStationAsync(seeded.FestivalId, seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(queue[0].Items.Select(item => item.ItemName),
                Is.EqualTo(new[]
                           {
                             "Käsekrainer",
                             "Käsekrainer",
                             "Schnitzel"
                           }));
    Assert.That(queue[0].Items.Select(item => item.Note),
                Is.EqualTo(new[]
                           {
                             null,
                             "Ohne Ketchup",
                             null
                           }));
  }

  [Test]
  public async Task FindUnfinishedAtStationAsync_AStationOrderThatIsCompletelyDone_LeavesItOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    IReadOnlyList<Guid> items = await PlaceOrderAsync(fixture, seeded, seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    await FulfillAsync(fixture, items);

    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<QueuedStationOrder> queue = await repository.FindUnfinishedAtStationAsync(seeded.FestivalId, seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(queue, Is.Empty);
  }

  [Test]
  public async Task FindUnfinishedAtStationAsync_AStationOrderOfAnotherStation_LeavesItOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await PlaceOrderAsync(fixture, seeded, seeded.BarStationId, "Tisch 12", 1, DeliveryMode.Together);

    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<QueuedStationOrder> queue = await repository.FindUnfinishedAtStationAsync(seeded.FestivalId, seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(queue, Is.Empty);
  }

  [Test]
  public async Task FindUnfinishedAtStationAsync_SeveralStationOrders_ListsThemInTheOrderTheStationNumbersThem()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await PlaceOrderAsync(fixture, seeded, seeded.KitchenStationId, "Tisch 12", 2, DeliveryMode.Together);
    await PlaceOrderAsync(fixture, seeded, seeded.KitchenStationId, "Tisch 3", 1, DeliveryMode.Together);

    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<QueuedStationOrder> queue = await repository.FindUnfinishedAtStationAsync(seeded.FestivalId, seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(queue.Select(stationOrder => stationOrder.TableName),
                Is.EqualTo(new[]
                           {
                             "Tisch 3",
                             "Tisch 12"
                           }));
  }

  [Test]
  public async Task FindFulfilledAtStationAsync_AStationOrderWithOneItemHandedOut_ListsItWithBothCounts()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    IReadOnlyList<Guid> items = await PlaceOrderAsync(fixture, seeded, seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    await FulfillAsync(fixture, [items[0]]);

    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<QueuedStationOrder> fulfilled = await repository.FindFulfilledAtStationAsync(seeded.FestivalId, seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(fulfilled, Has.Count.EqualTo(1));
                      Assert.That(fulfilled[0].ItemCount, Is.EqualTo(2));
                      Assert.That(fulfilled[0].FulfilledItemCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task FindFulfilledAtStationAsync_AStationOrderNobodyHasTouched_LeavesItOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await PlaceOrderAsync(fixture, seeded, seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);

    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<QueuedStationOrder> fulfilled = await repository.FindFulfilledAtStationAsync(seeded.FestivalId, seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(fulfilled, Is.Empty);
  }

  [Test]
  public async Task FindItemsAtStationAsync_ItemsOfAnotherStation_ReturnsOnlyTheOnesOfThisStation()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    IReadOnlyList<Guid> kitchenItems = await PlaceOrderAsync(fixture, seeded, seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    IReadOnlyList<Guid> barItems = await PlaceOrderAsync(fixture, seeded, seeded.BarStationId, "Tisch 12", 2, DeliveryMode.Together);

    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<OrderItem> items = await repository.FindItemsAtStationAsync(kitchenItems.Concat(barItems).ToList(), seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(items.Select(item => item.Id), Is.EquivalentTo(kitchenItems));
  }

  [Test]
  public async Task FindItemsAtStationAsync_TheItemsOfThisStation_ReturnsThemSoTheirChangesCanBeSaved()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    IReadOnlyList<Guid> items = await PlaceOrderAsync(fixture, seeded, seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);

    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<OrderItem> found = await repository.FindItemsAtStationAsync([items[0]], seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    found[0].FulfilledAtUtc = _orderedAtUtc.AddMinutes(5);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();
    var stored = await readContext.OrderItems.FirstAsync(item => item.Id == items[0], TestContext.CurrentContext.CancellationToken);

    Assert.That(stored.FulfilledAtUtc, Is.EqualTo(_orderedAtUtc.AddMinutes(5)));
  }

  [Test]
  public async Task FindAtStationAsync_AStationOrderOfAnotherStation_FindsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await PlaceOrderAsync(fixture, seeded, seeded.BarStationId, "Tisch 12", 1, DeliveryMode.AsItComes);
    var barStationOrderId = await StationOrderIdAsync(fixture, seeded.BarStationId);

    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    var found = await repository.FindAtStationAsync(barStationOrderId, seeded.KitchenStationId, seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.Null);
  }

  [Test]
  public async Task FindAtStationAsync_AStationOrderOfThisStation_ReturnsItSoItsChangesCanBeSaved()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await PlaceOrderAsync(fixture, seeded, seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.AsItComes);
    var stationOrderId = await StationOrderIdAsync(fixture, seeded.KitchenStationId);

    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    var found = await repository.FindAtStationAsync(stationOrderId, seeded.KitchenStationId, seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    found!.IsHiddenFromAsItComesQueue = true;
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();
    var stored = await readContext.StationOrders.FirstAsync(stationOrder => stationOrder.Id == stationOrderId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stored.IsHiddenFromAsItComesQueue, Is.True);
  }

  [Test]
  public async Task FindOrderIdsOfStationOrdersAsync_TwoStationOrdersOfOneOrder_NamesThatOrderOnce()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await PlaceOrderAsync(fixture, seeded, seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    var orderId = await OnlyOrderIdAsync(fixture);
    await AddStationOrderAsync(fixture, seeded, orderId, seeded.BarStationId);

    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());
    List<Guid> stationOrderIds = await StationOrderIdsAsync(fixture);

    IReadOnlyList<Guid> orderIds = await repository.FindOrderIdsOfStationOrdersAsync(stationOrderIds, TestContext.CurrentContext.CancellationToken);

    Assert.That(orderIds, Is.EqualTo(new[] { orderId }));
  }

  [Test]
  public async Task FindOrdersWithItemsAsync_AnOrderAcrossTwoStations_CarriesEveryItemOfThatOrder()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    IReadOnlyList<Guid> kitchenItems = await PlaceOrderAsync(fixture, seeded, seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    var orderId = await OnlyOrderIdAsync(fixture);
    await AddStationOrderAsync(fixture, seeded, orderId, seeded.BarStationId);
    await FulfillAsync(fixture, [kitchenItems[0]]);

    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<Order> orders = await repository.FindOrdersWithItemsAsync([orderId], TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(orders, Has.Count.EqualTo(1));
                      Assert.That(orders[0].StationOrders.SelectMany(stationOrder => stationOrder.Items).Count(), Is.EqualTo(4));
                      Assert.That(orders[0].StationOrders.SelectMany(stationOrder => stationOrder.Items).Count(item => item.FulfilledAtUtc != null), Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task FindOrdersWithItemsAsync_AnOrderThatIsNoLongerThere_ReportsNothingForIt()
  {
    using SqliteInMemoryFixture fixture = new();
    await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<Order> orders = await repository.FindOrdersWithItemsAsync([Guid.NewGuid()], TestContext.CurrentContext.CancellationToken);

    Assert.That(orders, Is.Empty);
  }

  [Test]
  public async Task FindQueuedWorkAtFestivalAsync_OpenItems_ReportsTheProductionTimeOfTheirArticlePerStation()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await GiveTheSausageAProductionTimeAsync(fixture, seeded);
    await PlaceOrderAsync(fixture, seeded, seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);

    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<StationQueuedWork> queuedWork = await repository.FindQueuedWorkAtFestivalAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(queuedWork, Has.Count.EqualTo(2));
                      Assert.That(queuedWork.Select(row => row.StationId), Is.All.EqualTo(seeded.KitchenStationId));
                      Assert.That(queuedWork.Select(row => row.Work.ProductionMinutes),
                                  Is.EquivalentTo(new double?[]
                                                  {
                                                    4,
                                                    null
                                                  }));
                    });
  }

  [Test]
  public async Task FindQueuedWorkAtFestivalAsync_AnItemTheStationHandedOut_LeavesItOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    IReadOnlyList<Guid> items = await PlaceOrderAsync(fixture, seeded, seeded.KitchenStationId, "Tisch 12", 1, DeliveryMode.Together);
    await FulfillAsync(fixture, items);

    StationOrderRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<StationQueuedWork> queuedWork = await repository.FindQueuedWorkAtFestivalAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(queuedWork, Is.Empty);
  }

  private async Task GiveTheSausageAProductionTimeAsync(SqliteInMemoryFixture fixture, SeededDomain seeded)
  {
    var sausage = await fixture.DbContext.CatalogItems.FirstAsync(item => item.Id == seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);
    sausage.ProductionMinutes = 4;
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task FulfillAsync(SqliteInMemoryFixture fixture, IReadOnlyCollection<Guid> orderItemIds)
  {
    List<Guid> ids = orderItemIds.ToList();
    List<OrderItem> items = await fixture.DbContext.OrderItems.Where(item => ids.Contains(item.Id)).ToListAsync(TestContext.CurrentContext.CancellationToken);

    foreach (var item in items)
      item.FulfilledAtUtc = _orderedAtUtc.AddMinutes(5);

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<Guid> OnlyOrderIdAsync(SqliteInMemoryFixture fixture)
  {
    return await fixture.DbContext.Orders.Select(order => order.Id).FirstAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<Guid> StationOrderIdAsync(SqliteInMemoryFixture fixture, Guid stationId)
  {
    return await fixture.DbContext.StationOrders.Where(stationOrder => stationOrder.StationId == stationId).Select(stationOrder => stationOrder.Id).FirstAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<List<Guid>> StationOrderIdsAsync(SqliteInMemoryFixture fixture)
  {
    return await fixture.DbContext.StationOrders.Select(stationOrder => stationOrder.Id).ToListAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task AddStationOrderAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, Guid orderId, Guid stationId)
  {
    var stationOrderId = Guid.NewGuid();

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = orderId,
                                  FestivalId = seeded.FestivalId,
                                  StationId = stationId,
                                  StationOrderNumber = 1,
                                  DeliveryMode = DeliveryMode.Together
                                };

    stationOrder.Items.Add(BuildItem(stationOrderId, seeded.LemonadeItemId, "Limonade", 250, null));
    stationOrder.Items.Add(BuildItem(stationOrderId, seeded.LemonadeItemId, "Limonade", 250, null));

    fixture.DbContext.StationOrders.Add(stationOrder);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<IReadOnlyList<Guid>> PlaceOrderAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, Guid stationId, string tableName, int orderNumber, DeliveryMode deliveryMode)
  {
    var orderId = Guid.NewGuid();
    var stationOrderId = Guid.NewGuid();

    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = seeded.FestivalId,
                    GlobalOrderNumber = orderNumber,
                    StaffMemberId = seeded.StaffMemberId,
                    TableName = tableName,
                    CreatedAtUtc = _orderedAtUtc
                  };

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = orderId,
                                  FestivalId = seeded.FestivalId,
                                  StationId = stationId,
                                  StationOrderNumber = orderNumber,
                                  DeliveryMode = deliveryMode
                                };

    stationOrder.Items.Add(BuildItem(stationOrderId, seeded.SausageItemId, "Bratwurst", 350, "Ohne Ketchup"));
    stationOrder.Items.Add(BuildItem(stationOrderId, seeded.LemonadeItemId, "Limonade", 250, null));
    order.StationOrders.Add(stationOrder);

    fixture.DbContext.Orders.Add(order);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return stationOrder.Items.Select(item => item.Id).ToList();
  }

  private OrderItem BuildItem(Guid stationOrderId, Guid catalogItemId, string itemName, int unitPriceCents, string? note)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = stationOrderId,
             CatalogItemId = catalogItemId,
             ItemName = itemName,
             UnitPriceCents = unitPriceCents,
             Note = note
           };
  }

  private OrderItem BuildItemWithId(Guid stationOrderId, Guid catalogItemId, string itemName, string id, string? note)
  {
    var item = BuildItem(stationOrderId, catalogItemId, itemName, 350, note);
    item.Id = new(id);
    return item;
  }

  private async Task<StationOrderRepository> AnOrderWithItemsAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, Guid stationOrderId, params OrderItem[] items)
  {
    var orderId = Guid.NewGuid();

    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = seeded.FestivalId,
                    GlobalOrderNumber = 1,
                    StaffMemberId = seeded.StaffMemberId,
                    TableName = "Tisch 12",
                    CreatedAtUtc = _orderedAtUtc
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

    foreach (var item in items)
      stationOrder.Items.Add(item);

    order.StationOrders.Add(stationOrder);
    fixture.DbContext.Orders.Add(order);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return new(fixture.DbContext, new ProjectionConfiguration().Build());
  }
}
