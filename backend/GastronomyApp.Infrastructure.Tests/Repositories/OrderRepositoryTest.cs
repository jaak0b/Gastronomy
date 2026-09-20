using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

public sealed class OrderRepositoryTest
{
  [Test]
  public async Task AddAsync_NewOrder_PersistsTheOrderItsStationOrdersAndItsItems()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    OrderRepository repository = new(fixture.DbContext);
    var order = BuildOrder(seeded, Guid.NewGuid());

    await repository.AddAsync(order, TestContext.CurrentContext.CancellationToken);

    var reloaded = await repository.FindByClientOrderIdAsync(order.ClientOrderId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(reloaded, Is.Not.Null);
                      Assert.That(reloaded!.StationOrders, Has.Count.EqualTo(1));
                      Assert.That(reloaded.StationOrders[0].DeliveryMode, Is.EqualTo(DeliveryMode.AsItComes));
                      Assert.That(reloaded.StationOrders[0].Items, Has.Count.EqualTo(1));
                      Assert.That(reloaded.StationOrders[0].Items[0].ItemName, Is.EqualTo("Bratwurst"));
                      Assert.That(reloaded.StationOrders[0].Items[0].UnitPriceCents, Is.EqualTo(350));
                      Assert.That(reloaded.StationOrders[0].Items[0].FulfilledAtUtc, Is.Null);
                    });
  }

  [Test]
  public async Task FindByClientOrderIdAsync_UnknownId_ReturnsNull()
  {
    using SqliteInMemoryFixture fixture = new();
    OrderRepository repository = new(fixture.DbContext);

    var found = await repository.FindByClientOrderIdAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.Null);
  }

  [Test]
  public async Task FindPlacedAsync_AnOrderThatIsNotThere_ReturnsNull()
  {
    using SqliteInMemoryFixture fixture = new();
    OrderRepository repository = new(fixture.DbContext);

    Assert.That(await repository.FindPlacedAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken), Is.Null);
  }

  [Test]
  public async Task FindPlacedAsync_AnOrderThatWasSent_CarriesItsStationOrdersWithTheStationNames()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    OrderRepository repository = new(fixture.DbContext);
    var order = BuildOrder(seeded, Guid.NewGuid());
    await repository.AddAsync(order, TestContext.CurrentContext.CancellationToken);

    var placed = (await repository.FindPlacedAsync(order.Id, TestContext.CurrentContext.CancellationToken))!;

    Assert.Multiple(() =>
                    {
                      Assert.That(placed.GlobalOrderNumber, Is.EqualTo(1));
                      Assert.That(placed.StationOrders, Has.Count.EqualTo(1));
                      Assert.That(placed.StationOrders[0].StationName, Is.EqualTo("Kueche"));
                      Assert.That(placed.StationOrders[0].DeliveryMode, Is.EqualTo(DeliveryMode.AsItComes));
                      Assert.That(placed.StationOrders[0].Items, Has.Count.EqualTo(1));
                      Assert.That(placed.StationOrders[0].Items[0].UnitPriceCents, Is.EqualTo(350));
                      Assert.That(placed.StationOrders[0].Items[0].IsFulfilled, Is.False);
                    });
  }

  [Test]
  public async Task FindPlacedAsync_AnItemTheStationHandedOut_SaysItIsFulfilled()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    OrderRepository repository = new(fixture.DbContext);
    var order = BuildOrder(seeded, Guid.NewGuid());
    order.StationOrders[0].Items[0].FulfilledAtUtc = new(2026, 8, 27, 18, 45, 0, DateTimeKind.Utc);
    await repository.AddAsync(order, TestContext.CurrentContext.CancellationToken);

    var placed = (await repository.FindPlacedAsync(order.Id, TestContext.CurrentContext.CancellationToken))!;

    Assert.That(placed.StationOrders[0].Items[0].IsFulfilled, Is.True);
  }

  [Test]
  public async Task FindPlacedAsync_TwoStationsWithTheSameOrderNumber_ListsThemInTheStationsOwnOrder()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    OrderRepository repository = new(fixture.DbContext);
    var order = BuildOrderForTwoStations(seeded, Guid.NewGuid());
    await repository.AddAsync(order, TestContext.CurrentContext.CancellationToken);

    var placed = (await repository.FindPlacedAsync(order.Id, TestContext.CurrentContext.CancellationToken))!;

    Assert.That(placed.StationOrders.Select(stationOrder => stationOrder.StationName),
                Is.EqualTo(new[]
                           {
                             "Kueche",
                             "Theke"
                           }));
  }

  [Test]
  public async Task FindPlacedAsync_AnItemAnotherWasOrderedBetween_ComeBackTogetherByNameAndNote()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    OrderRepository repository = new(fixture.DbContext);
    var order = BuildOrderWithItems(seeded, Guid.NewGuid());
    await repository.AddAsync(order, TestContext.CurrentContext.CancellationToken);

    var placed = (await repository.FindPlacedAsync(order.Id, TestContext.CurrentContext.CancellationToken))!;

    Assert.That(placed.StationOrders[0].Items.Select(item => item.OrderItemId),
                Is.EqualTo(new[]
                           {
                             _firstKasekrainerId,
                             _secondKasekrainerId,
                             _schnitzelId
                           }));
  }

  private readonly Guid _firstKasekrainerId = new("00000000-0000-0000-0000-000000000002");
  private readonly Guid _secondKasekrainerId = new("00000000-0000-0000-0000-000000000003");
  private readonly Guid _schnitzelId = new("00000000-0000-0000-0000-000000000001");

  private Order BuildOrderForTwoStations(SeededDomain seeded, Guid clientOrderId)
  {
    Order order = BuildOrder(seeded, clientOrderId);
    order.StationOrders.Clear();
    order.StationOrders.Add(BuildStationOrder(seeded, order.Id, seeded.BarStationId, new("00000000-0000-0000-0000-000000000001")));
    order.StationOrders.Add(BuildStationOrder(seeded, order.Id, seeded.KitchenStationId, new("00000000-0000-0000-0000-000000000002")));

    return order;
  }

  private Order BuildOrderWithItems(SeededDomain seeded, Guid clientOrderId)
  {
    Order order = BuildOrder(seeded, clientOrderId);
    StationOrder stationOrder = order.StationOrders[0];
    stationOrder.Items.Clear();
    stationOrder.Items.Add(BuildItem(stationOrder.Id, seeded.SausageItemId, "Käsekrainer", _firstKasekrainerId));
    stationOrder.Items.Add(BuildItem(stationOrder.Id, seeded.LemonadeItemId, "Schnitzel", _schnitzelId));
    stationOrder.Items.Add(BuildItem(stationOrder.Id, seeded.SausageItemId, "Käsekrainer", _secondKasekrainerId));

    return order;
  }

  private StationOrder BuildStationOrder(SeededDomain seeded, Guid orderId, Guid stationId, Guid stationOrderId)
  {
    return new()
           {
             Id = stationOrderId,
             OrderId = orderId,
             FestivalId = seeded.FestivalId,
             StationId = stationId,
             StationOrderNumber = 1,
             DeliveryMode = DeliveryMode.AsItComes
           };
  }

  private OrderItem BuildItem(Guid stationOrderId, Guid catalogItemId, string itemName, Guid itemId)
  {
    return new()
           {
             Id = itemId,
             StationOrderId = stationOrderId,
             CatalogItemId = catalogItemId,
             ItemName = itemName,
             UnitPriceCents = 350,
             Note = null
           };
  }

  private Order BuildOrder(SeededDomain seeded, Guid clientOrderId)
  {
    DateTime createdAtUtc = new(2026, 8, 27, 18, 30, 0, DateTimeKind.Utc);
    var orderId = Guid.NewGuid();
    var stationOrderId = Guid.NewGuid();

    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = clientOrderId,
                    FestivalId = seeded.FestivalId,
                    GlobalOrderNumber = 1,
                    StaffMemberId = seeded.StaffMemberId,
                    TableName = "Tisch 12",
                    Note = null,
                    CreatedAtUtc = createdAtUtc
                  };

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = orderId,
                                  FestivalId = seeded.FestivalId,
                                  StationId = seeded.KitchenStationId,
                                  StationOrderNumber = 1,
                                  DeliveryMode = DeliveryMode.AsItComes
                                };

    OrderItem item = new()
                     {
                       Id = Guid.NewGuid(),
                       StationOrderId = stationOrderId,
                       CatalogItemId = seeded.SausageItemId,
                       ItemName = "Bratwurst",
                       UnitPriceCents = 350,
                       Note = null
                     };

    stationOrder.Items.Add(item);
    order.StationOrders.Add(stationOrder);

    return order;
  }
}
