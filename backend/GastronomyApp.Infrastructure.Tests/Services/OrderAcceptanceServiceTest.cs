using ErrorOr;
using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Services;

public sealed class OrderAcceptanceServiceTest
{
  [Test]
  public async Task AcceptAsync_NewClientOrderId_InsertsOrderTicketsAndLines()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var acceptanceService = new OrderAcceptanceComposition().Create(fixture.DbContext);

    ErrorOr<Order> result = await acceptanceService.AcceptAsync(BuildRequest(seeded, Guid.NewGuid()), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(result.Value.GlobalOrderNumber, Is.EqualTo(1));
                      Assert.That(result.Value.StationOrders, Has.Count.EqualTo(2));
                      Assert.That(result.Value.StationOrders.SelectMany(stationOrder => stationOrder.Items).Count(), Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task AcceptAsync_RepeatClientOrderId_ReturnsExistingOrderAndInsertsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var acceptanceService = new OrderAcceptanceComposition().Create(fixture.DbContext);
    var clientOrderId = Guid.NewGuid();

    ErrorOr<Order> first = await acceptanceService.AcceptAsync(BuildRequest(seeded, clientOrderId), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    ErrorOr<Order> second = await acceptanceService.AcceptAsync(BuildRequest(seeded, clientOrderId), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    var orderCount = await fixture.DbContext.Orders.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(second.IsSuccess, Is.True);
                      Assert.That(second.Value.Id, Is.EqualTo(first.Value.Id));
                      Assert.That(orderCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task AcceptAsync_MultipleStations_AllocatesIndependentSequenceNumbers()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var acceptanceService = new OrderAcceptanceComposition().Create(fixture.DbContext);

    await acceptanceService.AcceptAsync(BuildRequest(seeded, Guid.NewGuid()), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    ErrorOr<Order> second = await acceptanceService.AcceptAsync(BuildRequest(seeded, Guid.NewGuid()), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    var kitchenTicket = second.Value.StationOrders.Single(stationOrder => stationOrder.StationId == seeded.KitchenStationId);
    var barTicket = second.Value.StationOrders.Single(stationOrder => stationOrder.StationId == seeded.BarStationId);

    Assert.Multiple(() =>
                    {
                      Assert.That(kitchenTicket.StationOrderNumber, Is.EqualTo(2));
                      Assert.That(barTicket.StationOrderNumber, Is.EqualTo(2));
                      Assert.That(second.Value.GlobalOrderNumber, Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task AcceptAsync_ASentAndSettledOrder_StoresTheChargesWithTheOrder()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var acceptanceService = new OrderAcceptanceComposition().Create(fixture.DbContext);

    ErrorOr<Order> result = await acceptanceService.AcceptAsync(BuildRequest(seeded, Guid.NewGuid(), new() { PaidPriceCents = 350 }, new() { PaidPriceCents = 350 }), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    List<OrderItem> storedItems = await fixture.DbContext.OrderItems.ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(storedItems, Has.Count.EqualTo(2));
                      Assert.That(storedItems.Select(item => item.ChargedPriceCents), Is.All.EqualTo(350));
                      Assert.That(storedItems.Select(item => item.SettledAtUtc), Is.All.EqualTo(result.Value.CreatedAtUtc));
                      Assert.That(storedItems.Select(item => item.SettledByStaffMemberId), Is.All.EqualTo(seeded.StaffMemberId));
                    });
  }

  [Test]
  public async Task AcceptAsync_OneLineSettledAndOneOpen_SettlesOnlyTheLineThatCarriesASettlement()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var acceptanceService = new OrderAcceptanceComposition().Create(fixture.DbContext);

    ErrorOr<Order> result = await acceptanceService.AcceptAsync(BuildRequest(seeded, Guid.NewGuid(), new() { PaidPriceCents = 350 }), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    List<OrderItem> storedItems = await fixture.DbContext.OrderItems.ToListAsync(TestContext.CurrentContext.CancellationToken);
    var settledLine = storedItems.Single(item => item.CatalogItemId == seeded.SausageItemId);
    var openLine = storedItems.Single(item => item.CatalogItemId == seeded.LemonadeItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(settledLine.ChargedPriceCents, Is.EqualTo(350));
                      Assert.That(settledLine.SettledAtUtc, Is.EqualTo(result.Value.CreatedAtUtc));
                      Assert.That(settledLine.SettledByStaffMemberId, Is.EqualTo(seeded.StaffMemberId));
                      Assert.That(openLine.ChargedPriceCents, Is.Null);
                      Assert.That(openLine.SettledAtUtc, Is.Null);
                      Assert.That(openLine.SettledByStaffMemberId, Is.Null);
                    });
  }

  [Test]
  public async Task AcceptAsync_ReplayingASettledClientOrderId_ReturnsTheStoredOrderWithoutWritingOrNumberingAnythingAgain()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var acceptanceService = new OrderAcceptanceComposition().Create(fixture.DbContext);
    var clientOrderId = Guid.NewGuid();
    OrderSettlementLineRequest sausageSettlement = new() { PaidPriceCents = 350 };
    OrderSettlementLineRequest lemonadeSettlement = new() { PaidPriceCents = 350 };

    ErrorOr<Order> first = await acceptanceService.AcceptAsync(BuildRequest(seeded, clientOrderId, sausageSettlement, lemonadeSettlement), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();
    ErrorOr<Order> second = await acceptanceService.AcceptAsync(BuildRequest(seeded, clientOrderId, sausageSettlement, lemonadeSettlement), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    using var verificationContext = fixture.CreateContext();
    List<OrderItem> storedItems = await verificationContext.OrderItems.ToListAsync(TestContext.CurrentContext.CancellationToken);
    var orderCount = await verificationContext.Orders.CountAsync(TestContext.CurrentContext.CancellationToken);
    var nextOrderNumber = await verificationContext.Festivals.Select(festival => festival.NextOrderNumber).SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(second.IsSuccess, Is.True);
                      Assert.That(second.Value.Id, Is.EqualTo(first.Value.Id));
                      Assert.That(orderCount, Is.EqualTo(1));
                      Assert.That(storedItems, Has.Count.EqualTo(2));
                      Assert.That(storedItems.Select(item => item.ChargedPriceCents), Is.All.EqualTo(350));
                      Assert.That(storedItems.Select(item => item.SettledAtUtc), Is.All.Not.Null);
                      Assert.That(nextOrderNumber, Is.EqualTo(2));
                    });
  }

  private PlaceOrderRequest BuildRequest(SeededDomain seeded, Guid clientOrderId, OrderSettlementLineRequest? sausageSettlement = null, OrderSettlementLineRequest? lemonadeSettlement = null)
  {
    return new()
    {
      ClientOrderId = clientOrderId,
      TableName = "Tisch 12",
      Items =
             [
               new()
               {
                 CatalogItemId = seeded.SausageItemId,
                 Note = null,
                 UnitPriceCents = 350,
                 Settlement = sausageSettlement
               },
               new()
               {
                 CatalogItemId = seeded.LemonadeItemId,
                 Note = null,
                 UnitPriceCents = 350,
                 Settlement = lemonadeSettlement
               }
             ]
    };
  }
}
