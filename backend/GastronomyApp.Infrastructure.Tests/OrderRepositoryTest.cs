using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class OrderRepositoryTest
{
  [Test]
  public async Task AddAsync_NewOrder_PersistsTheOrderItsStationOrdersAndItsItems()
  {
    using SqliteInMemoryFixture fixture = new();
    SeededDomain seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    OrderRepository repository = new(fixture.DbContext);
    Order order = BuildOrder(seeded, Guid.NewGuid());

    await repository.AddAsync(order, TestContext.CurrentContext.CancellationToken);

    Order? reloaded = await repository.FindByClientOrderIdAsync(order.ClientOrderId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
    {
      Assert.That(reloaded, Is.Not.Null);
      Assert.That(reloaded!.StationOrders, Has.Count.EqualTo(1));
      Assert.That(reloaded.StationOrders[0].Items, Has.Count.EqualTo(1));
      Assert.That(reloaded.StationOrders[0].Items[0].ItemName, Is.EqualTo("Bratwurst"));
      Assert.That(reloaded.StationOrders[0].Items[0].UnitPriceCents, Is.EqualTo(350));
      Assert.That(reloaded.StationOrders[0].PrintJobs, Has.Count.EqualTo(1));
      Assert.That(reloaded.StationOrders[0].PrintJobs[0].CopyNumber, Is.EqualTo(0));
    });
  }

  [Test]
  public async Task FindByClientOrderIdAsync_UnknownId_ReturnsNull()
  {
    using SqliteInMemoryFixture fixture = new();
    OrderRepository repository = new(fixture.DbContext);

    Order? found = await repository.FindByClientOrderIdAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.Null);
  }


  private Order BuildOrder(SeededDomain seeded, Guid clientOrderId)
  {
    DateTime createdAtUtc = new(2026, 8, 27, 18, 30, 0, DateTimeKind.Utc);
    Guid orderId = Guid.NewGuid();
    Guid stationOrderId = Guid.NewGuid();

    Order order = new()
    {
      Id = orderId,
      ClientOrderId = clientOrderId,
      GlobalOrderNumber = 1,
      StaffMemberId = seeded.StaffMemberId,
      TableName = "Tisch 12",
      Note = null,
      CreatedAtUtc = createdAtUtc,
    };

    StationOrder stationOrder = new()
    {
      Id = stationOrderId,
      OrderId = orderId,
      StationId = seeded.KitchenStationId,
      StationOrderNumber = 1,
    };

    stationOrder.PrintJobs.Add(new PrintJob
    {
      Id = Guid.NewGuid(),
      StationOrderId = stationOrderId,
      CopyNumber = 0,
      Status = PrintJobStatus.Queued,
      CreatedAtUtc = createdAtUtc,
    });

    stationOrder.Items.Add(new OrderItem
    {
      Id = Guid.NewGuid(),
      StationOrderId = stationOrderId,
      CatalogItemId = seeded.SausageItemId,
      ItemName = "Bratwurst",
      UnitPriceCents = 350,
      Note = null,
    });

    order.StationOrders.Add(stationOrder);

    return order;
  }
}
