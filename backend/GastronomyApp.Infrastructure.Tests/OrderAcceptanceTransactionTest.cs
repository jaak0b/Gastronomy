using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class OrderAcceptanceTransactionTest
{
  [Test]
  public async Task AcceptAsync_NewClientOrderId_InsertsOrderTicketsAndLines()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var transaction = new OrderAcceptanceComposition().Create(fixture.DbContext);

    Result<OrderAcceptanceResult, OrderValidationFailure> result = await transaction.AcceptAsync(BuildRequest(seeded, Guid.NewGuid()),
                                                                                                 TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(result.Value.WasAlreadyAccepted, Is.False);
                      Assert.That(result.Value.Order.GlobalOrderNumber, Is.EqualTo(1));
                      Assert.That(result.Value.Order.StationOrders, Has.Count.EqualTo(2));
                      Assert.That(result.Value.Order.StationOrders.SelectMany(stationOrder => stationOrder.Items).Count(), Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task AcceptAsync_RepeatClientOrderId_ReturnsExistingOrderAndInsertsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var transaction = new OrderAcceptanceComposition().Create(fixture.DbContext);
    var clientOrderId = Guid.NewGuid();

    Result<OrderAcceptanceResult, OrderValidationFailure> first = await transaction.AcceptAsync(BuildRequest(seeded, clientOrderId), TestContext.CurrentContext.CancellationToken);
    Result<OrderAcceptanceResult, OrderValidationFailure> second = await transaction.AcceptAsync(BuildRequest(seeded, clientOrderId), TestContext.CurrentContext.CancellationToken);

    var orderCount = await fixture.DbContext.Orders.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(second.IsSuccess, Is.True);
                      Assert.That(second.Value.WasAlreadyAccepted, Is.True);
                      Assert.That(second.Value.Order.Id, Is.EqualTo(first.Value.Order.Id));
                      Assert.That(orderCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task AcceptAsync_TwoParallelSubmissionsSameClientOrderId_ProduceExactlyOneOrder()
  {
    using SqliteTempFileFixture fixture = new();
    var seedContext = fixture.CreateContext();
    var seeded = await new DomainSeeder().SeedAsync(seedContext, TestContext.CurrentContext.CancellationToken);
    var clientOrderId = Guid.NewGuid();

    OrderAcceptanceComposition composition = new();
    var firstTransaction = composition.Create(fixture.CreateContext());
    var secondTransaction = composition.Create(fixture.CreateContext());

    Task<Result<OrderAcceptanceResult, OrderValidationFailure>> firstCall =
      Task.Run(() => firstTransaction.AcceptAsync(BuildRequest(seeded, clientOrderId), TestContext.CurrentContext.CancellationToken));
    Task<Result<OrderAcceptanceResult, OrderValidationFailure>> secondCall =
      Task.Run(() => secondTransaction.AcceptAsync(BuildRequest(seeded, clientOrderId), TestContext.CurrentContext.CancellationToken));

    Result<OrderAcceptanceResult, OrderValidationFailure>[] results = await Task.WhenAll(firstCall, secondCall);

    var verificationContext = fixture.CreateContext();
    var orderCount = await verificationContext.Orders.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(results[0].IsSuccess, Is.True);
                      Assert.That(results[1].IsSuccess, Is.True);
                      Assert.That(results[1].Value.Order.Id, Is.EqualTo(results[0].Value.Order.Id));
                      Assert.That(results.Count(result => result.Value.WasAlreadyAccepted),
                                  Is.EqualTo(1));
                      Assert.That(orderCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task AcceptAsync_MultipleStations_AllocatesIndependentSequenceNumbers()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var transaction = new OrderAcceptanceComposition().Create(fixture.DbContext);

    await transaction.AcceptAsync(BuildRequest(seeded, Guid.NewGuid()), TestContext.CurrentContext.CancellationToken);
    Result<OrderAcceptanceResult, OrderValidationFailure> second = await transaction.AcceptAsync(BuildRequest(seeded, Guid.NewGuid()), TestContext.CurrentContext.CancellationToken);

    var kitchenTicket = second.Value.Order.StationOrders.Single(stationOrder => stationOrder.StationId == seeded.KitchenStationId);
    var barTicket = second.Value.Order.StationOrders.Single(stationOrder => stationOrder.StationId == seeded.BarStationId);

    Assert.Multiple(() =>
                    {
                      Assert.That(kitchenTicket.StationOrderNumber, Is.EqualTo(2));
                      Assert.That(barTicket.StationOrderNumber, Is.EqualTo(2));
                      Assert.That(second.Value.Order.GlobalOrderNumber, Is.EqualTo(2));
                    });
  }

  private OrderAcceptanceRequest BuildRequest(SeededDomain seeded, Guid clientOrderId)
  {
    return new()
           {
             ClientOrderId = clientOrderId,
             StaffMemberId = seeded.StaffMemberId,
             TableName = "Tisch 12",
             Note = null,
             SettleOnSend = false,
             Items =
             [
               new()
               {
                 CatalogItemId = seeded.SausageItemId, Note = null,
                 UnitPriceCents = 350
               },
               new()
               {
                 CatalogItemId = seeded.LemonadeItemId, Note = null,
                 UnitPriceCents = 350
               }
             ]
           };
  }
}
