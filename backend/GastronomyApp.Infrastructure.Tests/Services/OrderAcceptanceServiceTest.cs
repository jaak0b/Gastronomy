using ErrorOr;
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
    var tableId = await new DomainSeeder().SeedTableAsync(fixture.DbContext, seeded.FestivalId, "Tisch 12", TestContext.CurrentContext.CancellationToken);
    var acceptanceService = new OrderAcceptanceComposition().Create(fixture.DbContext);

    ErrorOr<Order> result = await acceptanceService.AcceptAsync(BuildRequest(seeded, tableId, Guid.NewGuid()), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

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
    var tableId = await new DomainSeeder().SeedTableAsync(fixture.DbContext, seeded.FestivalId, "Tisch 12", TestContext.CurrentContext.CancellationToken);
    var acceptanceService = new OrderAcceptanceComposition().Create(fixture.DbContext);
    var clientOrderId = Guid.NewGuid();

    ErrorOr<Order> first = await acceptanceService.AcceptAsync(BuildRequest(seeded, tableId, clientOrderId), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    ErrorOr<Order> second = await acceptanceService.AcceptAsync(BuildRequest(seeded, tableId, clientOrderId), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    var orderCount = await fixture.DbContext.Orders.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(second.IsSuccess, Is.True);
                      Assert.That(second.Value.Id, Is.EqualTo(first.Value.Id));
                      Assert.That(orderCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task AcceptAsync_ATableOfAnotherFestival_IsRefusedAndStoresNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    DomainSeeder seeder = new();
    var seeded = await seeder.SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var otherFestivalId = Guid.NewGuid();
    fixture.DbContext.Festivals.Add(new()
                                    {
                                      Id = otherFestivalId,
                                      Name = "Herbstfest",
                                      StartsAtUtc = new(2030, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                                      EndsAtUtc = new(2030, 10, 2, 12, 0, 0, DateTimeKind.Utc),
                                      NextOrderNumber = 1,
                                      IsHidden = false
                                    });
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    var otherFestivalsTableId = await seeder.SeedTableAsync(fixture.DbContext, otherFestivalId, "Tisch 12", TestContext.CurrentContext.CancellationToken);
    var acceptanceService = new OrderAcceptanceComposition().Create(fixture.DbContext);

    ErrorOr<Order> result = await acceptanceService.AcceptAsync(BuildRequest(seeded, otherFestivalsTableId, Guid.NewGuid()), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    var orderCount = await fixture.DbContext.Orders.CountAsync(TestContext.CurrentContext.CancellationToken);
    var festival = await fixture.DbContext.Festivals.AsNoTracking().SingleAsync(candidate => candidate.Id == seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsError, Is.True);
                      Assert.That(result.FirstError.Code, Is.EqualTo("errors.order.cannotBeProcessed"));
                      Assert.That(orderCount, Is.Zero);
                      Assert.That(festival.NextOrderNumber, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task AcceptAsync_MultipleStations_AllocatesIndependentSequenceNumbers()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var tableId = await new DomainSeeder().SeedTableAsync(fixture.DbContext, seeded.FestivalId, "Tisch 12", TestContext.CurrentContext.CancellationToken);
    var acceptanceService = new OrderAcceptanceComposition().Create(fixture.DbContext);

    await acceptanceService.AcceptAsync(BuildRequest(seeded, tableId, Guid.NewGuid()), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    ErrorOr<Order> second = await acceptanceService.AcceptAsync(BuildRequest(seeded, tableId, Guid.NewGuid()), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

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
  public async Task AcceptAsync_ReplayingAClientOrderIdFromAFreshContext_ReturnsTheStoredOrderWithoutWritingOrNumberingAnythingAgain()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var tableId = await new DomainSeeder().SeedTableAsync(fixture.DbContext, seeded.FestivalId, "Tisch 12", TestContext.CurrentContext.CancellationToken);
    var acceptanceService = new OrderAcceptanceComposition().Create(fixture.DbContext);
    var clientOrderId = Guid.NewGuid();

    ErrorOr<Order> first = await acceptanceService.AcceptAsync(BuildRequest(seeded, tableId, clientOrderId), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();
    ErrorOr<Order> second = await acceptanceService.AcceptAsync(BuildRequest(seeded, tableId, clientOrderId), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

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
                      Assert.That(nextOrderNumber, Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task AcceptAsync_TheOrderLeavesLessThanOnePortionOfAnIngredient_StoresTheArticleAsSoldOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var tableId = await new DomainSeeder().SeedTableAsync(fixture.DbContext, seeded.FestivalId, "Tisch 12", TestContext.CurrentContext.CancellationToken);
    Unit unit = new()
                {
                  Id = Guid.NewGuid(),
                  Name = "Portion",
                  IsActive = true
                };
    Ingredient mustard = new()
                         {
                           Id = Guid.NewGuid(),
                           Name = "Senf",
                           UnitId = unit.Id,
                           Unit = unit,
                           IsActive = true
                         };
    mustard.FestivalIngredients.Add(new()
                                    {
                                      Id = Guid.NewGuid(),
                                      FestivalId = seeded.FestivalId,
                                      IngredientId = mustard.Id,
                                      AvailableAmount = 30
                                    });
    fixture.DbContext.Units.Add(unit);
    fixture.DbContext.Ingredients.Add(mustard);
    fixture.DbContext.CatalogItemIngredients.Add(new()
                                                 {
                                                   Id = Guid.NewGuid(),
                                                   CatalogItemId = seeded.SausageItemId,
                                                   IngredientId = mustard.Id,
                                                   Amount = 20
                                                 });
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    var acceptanceService = new OrderAcceptanceComposition().Create(fixture.DbContext);

    ErrorOr<Order> result = await acceptanceService.AcceptAsync(BuildRequest(seeded, tableId, Guid.NewGuid()), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    var sausageRow = await fixture.DbContext.FestivalCatalogItems.SingleAsync(menuRow => menuRow.CatalogItemId == seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);
    var lemonadeRow = await fixture.DbContext.FestivalCatalogItems.SingleAsync(menuRow => menuRow.CatalogItemId == seeded.LemonadeItemId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(sausageRow.IsAvailable, Is.False);
                      Assert.That(lemonadeRow.IsAvailable, Is.True);
                    });
  }

  private PlaceOrderRequest BuildRequest(SeededDomain seeded, Guid tableId, Guid clientOrderId)
  {
    return new()
           {
             ClientOrderId = clientOrderId,
             TableId = tableId,
             Items =
             [
               new()
               {
                 CatalogItemId = seeded.SausageItemId,
                 Note = null,
                 UnitPriceCents = 350
               },
               new()
               {
                 CatalogItemId = seeded.LemonadeItemId,
                 Note = null,
                 UnitPriceCents = 350
               }
             ]
           };
  }
}
