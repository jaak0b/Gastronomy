using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
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
  public async Task FindWithStationOrdersAsync_AnOrderThatIsNotThere_ReturnsNull()
  {
    using SqliteInMemoryFixture fixture = new();
    OrderRepository repository = new(fixture.DbContext);

    Assert.That(await repository.FindWithStationOrdersAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken), Is.Null);
  }

  [Test]
  public async Task FindWithStationOrdersAsync_AnOrderThatWasSent_CarriesItsStationOrdersWithTheirStationAndItems()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    OrderRepository repository = new(fixture.DbContext);
    var order = BuildOrder(seeded, Guid.NewGuid());
    await repository.AddAsync(order, TestContext.CurrentContext.CancellationToken);

    var found = (await repository.FindWithStationOrdersAsync(order.Id, TestContext.CurrentContext.CancellationToken))!;

    Assert.Multiple(() =>
                    {
                      Assert.That(found.GlobalOrderNumber, Is.EqualTo(1));
                      Assert.That(found.StationOrders, Has.Count.EqualTo(1));
                      Assert.That(found.StationOrders[0].Station.Name, Is.EqualTo("Kueche"));
                      Assert.That(found.StationOrders[0].DeliveryMode, Is.EqualTo(DeliveryMode.AsItComes));
                      Assert.That(found.StationOrders[0].Items, Has.Count.EqualTo(1));
                      Assert.That(found.StationOrders[0].Items[0].UnitPriceCents, Is.EqualTo(350));
                      Assert.That(found.StationOrders[0].Items[0].CatalogItem, Is.Not.Null);
                      Assert.That(found.StationOrders[0].Items[0].FulfilledAtUtc, Is.Null);
                    });
  }

  [Test]
  public async Task FindWithStationOrdersAsync_AnItemTheStationHandedOut_CarriesTheMomentItLeft()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    OrderRepository repository = new(fixture.DbContext);
    var order = BuildOrder(seeded, Guid.NewGuid());
    var handedOutAtUtc = new DateTime(2026, 8, 27, 18, 45, 0, DateTimeKind.Utc);
    order.StationOrders[0].Items[0].FulfilledAtUtc = handedOutAtUtc;
    await repository.AddAsync(order, TestContext.CurrentContext.CancellationToken);

    var found = (await repository.FindWithStationOrdersAsync(order.Id, TestContext.CurrentContext.CancellationToken))!;

    Assert.That(found.StationOrders[0].Items[0].FulfilledAtUtc, Is.EqualTo(handedOutAtUtc));
  }

  [Test]
  public async Task FindWithStationOrdersAsync_AnOrderAtTwoStations_CarriesBothStationOrders()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    OrderRepository repository = new(fixture.DbContext);
    var order = BuildOrderForTwoStations(seeded, Guid.NewGuid());
    await repository.AddAsync(order, TestContext.CurrentContext.CancellationToken);

    var found = (await repository.FindWithStationOrdersAsync(order.Id, TestContext.CurrentContext.CancellationToken))!;

    Assert.That(found.StationOrders.Select(stationOrder => stationOrder.Station.Name),
                Is.EquivalentTo(new[]
                                {
                                  "Kueche",
                                  "Theke"
                                }));
  }

  private Order BuildOrderForTwoStations(SeededDomain seeded, Guid clientOrderId)
  {
    var order = BuildOrder(seeded, clientOrderId);
    order.StationOrders.Clear();
    order.StationOrders.Add(BuildStationOrder(seeded, order.Id, seeded.BarStationId, new("00000000-0000-0000-0000-000000000001")));
    order.StationOrders.Add(BuildStationOrder(seeded, order.Id, seeded.KitchenStationId, new("00000000-0000-0000-0000-000000000002")));

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
