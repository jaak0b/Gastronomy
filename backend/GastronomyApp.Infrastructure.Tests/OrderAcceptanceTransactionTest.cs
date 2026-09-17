using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

  [Test]
  public async Task AcceptAsync_CounterMovedOnAfterThisContextReadIt_RetriesAndTakesTheNumberThatFollowsIt()
  {
    using SqliteTempFileFixture fixture = new();
    var seedContext = fixture.CreateContext();
    var seeded = await new DomainSeeder().SeedAsync(seedContext, TestContext.CurrentContext.CancellationToken);

    var acceptanceContext = fixture.CreateContext();
    await acceptanceContext.Festivals.FirstAsync(candidate => candidate.Id == seeded.FestivalId,
                                                 TestContext.CurrentContext.CancellationToken);

    var competingContext = fixture.CreateContext();
    var competingFestival = await competingContext.Festivals.FirstAsync(candidate => candidate.Id == seeded.FestivalId,
                                                                        TestContext.CurrentContext.CancellationToken);
    competingFestival.NextOrderNumber = 5;
    await competingContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var transaction = new OrderAcceptanceComposition().Create(acceptanceContext);

    Result<OrderAcceptanceResult, OrderValidationFailure> result =
      await transaction.AcceptAsync(BuildRequest(seeded, Guid.NewGuid()), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(result.Value.Order.GlobalOrderNumber, Is.EqualTo(5));
                    });
  }

  [Test]
  public async Task AcceptAsync_EveryAttemptLosesTheCounter_RefusesTheOrderAndWritesNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    var alwaysContendedAllocator = A.Fake<INumberAllocator>();
    A.CallTo(() => alwaysContendedAllocator.AllocateGlobalOrderNumberAsync(A<Guid>._, A<CancellationToken>._))
     .ThrowsAsync(new DbUpdateConcurrencyException());

    var transaction = new OrderAcceptanceComposition().Create(fixture.DbContext, alwaysContendedAllocator);

    Result<OrderAcceptanceResult, OrderValidationFailure> result =
      await transaction.AcceptAsync(BuildRequest(seeded, Guid.NewGuid()), TestContext.CurrentContext.CancellationToken);

    var orderCount = await fixture.DbContext.Orders.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.False);
                      Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.OrderNumberCouldNotBeAllocated));
                      Assert.That(orderCount, Is.EqualTo(0));
                      A.CallTo(() => alwaysContendedAllocator.AllocateGlobalOrderNumberAsync(A<Guid>._, A<CancellationToken>._))
                       .MustHaveHappened(5, Times.Exactly);
                    });
  }

  [Test]
  public async Task AcceptAsync_AnAttemptLosesTheCounter_WritesAWarningNamingTheAttempt()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    var alwaysContendedAllocator = A.Fake<INumberAllocator>();
    A.CallTo(() => alwaysContendedAllocator.AllocateGlobalOrderNumberAsync(A<Guid>._, A<CancellationToken>._))
     .ThrowsAsync(new DbUpdateConcurrencyException());

    var logger = A.Fake<ILogger<OrderAcceptanceTransaction>>();
    var transaction = new OrderAcceptanceComposition().Create(fixture.DbContext, alwaysContendedAllocator, logger);

    await transaction.AcceptAsync(BuildRequest(seeded, Guid.NewGuid()), TestContext.CurrentContext.CancellationToken);

    A.CallTo(logger)
     .Where(call => call.Method.Name == nameof(ILogger.Log)
                    && call.GetArgument<LogLevel>(0) == LogLevel.Warning)
     .MustHaveHappened(4, Times.Exactly);
  }

  [Test]
  public async Task AcceptAsync_ASentAndSettledOrder_StoresTheChargesWithTheOrder()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var transaction = new OrderAcceptanceComposition().Create(fixture.DbContext);

    Result<OrderAcceptanceResult, OrderValidationFailure> result =
      await transaction.AcceptAsync(BuildRequest(seeded, Guid.NewGuid(), new() { AmountPaidCents = 700 }),
                                    TestContext.CurrentContext.CancellationToken);

    List<OrderItem> storedItems = await fixture.DbContext.OrderItems
                                                         .ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.True);
                      Assert.That(result.Value.WasAlreadyAccepted, Is.False);
                      Assert.That(storedItems, Has.Count.EqualTo(2));
                      Assert.That(storedItems.Select(item => item.ChargedPriceCents), Is.All.EqualTo(350));
                      Assert.That(storedItems.Select(item => item.SettledAtUtc), Is.All.EqualTo(result.Value.Order.CreatedAtUtc));
                      Assert.That(storedItems.Select(item => item.SettledByStaffMemberId), Is.All.EqualTo(seeded.StaffMemberId));
                    });
  }

  [Test]
  public async Task AcceptAsync_ASettlementBelowTheTotalWithoutANotice_RollsBackTheOrderAndItsNumbers()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var transaction = new OrderAcceptanceComposition().Create(fixture.DbContext);

    Result<OrderAcceptanceResult, OrderValidationFailure> result =
      await transaction.AcceptAsync(BuildRequest(seeded, Guid.NewGuid(), new() { AmountPaidCents = 500 }),
                                    TestContext.CurrentContext.CancellationToken);

    using var verificationContext = fixture.CreateContext();
    var orderCount = await verificationContext.Orders.CountAsync(TestContext.CurrentContext.CancellationToken);
    var itemCount = await verificationContext.OrderItems.CountAsync(TestContext.CurrentContext.CancellationToken);
    var nextOrderNumber = await verificationContext.Festivals
                                                   .Select(festival => festival.NextOrderNumber)
                                                   .SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(result.IsSuccess, Is.False);
                      Assert.That(result.Failure.Reason, Is.EqualTo(OrderValidationFailureReason.SettlementCannotBeProcessed));
                      Assert.That(orderCount, Is.Zero);
                      Assert.That(itemCount, Is.Zero);
                      Assert.That(nextOrderNumber, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task AcceptAsync_ReplayingASettledClientOrderId_ReturnsTheStoredOrderWithoutWritingOrNumberingAnythingAgain()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var transaction = new OrderAcceptanceComposition().Create(fixture.DbContext);
    var clientOrderId = Guid.NewGuid();
    OrderSettlementTerms settlement = new() { AmountPaidCents = 700 };

    Result<OrderAcceptanceResult, OrderValidationFailure> first =
      await transaction.AcceptAsync(BuildRequest(seeded, clientOrderId, settlement),
                                    TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();
    Result<OrderAcceptanceResult, OrderValidationFailure> second =
      await transaction.AcceptAsync(BuildRequest(seeded, clientOrderId, settlement),
                                    TestContext.CurrentContext.CancellationToken);

    using var verificationContext = fixture.CreateContext();
    List<OrderItem> storedItems = await verificationContext.OrderItems
                                                           .ToListAsync(TestContext.CurrentContext.CancellationToken);
    var orderCount = await verificationContext.Orders.CountAsync(TestContext.CurrentContext.CancellationToken);
    var nextOrderNumber = await verificationContext.Festivals
                                                   .Select(festival => festival.NextOrderNumber)
                                                   .SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(second.IsSuccess, Is.True);
                      Assert.That(second.Value.WasAlreadyAccepted, Is.True);
                      Assert.That(second.Value.Order.Id, Is.EqualTo(first.Value.Order.Id));
                      Assert.That(orderCount, Is.EqualTo(1));
                      Assert.That(storedItems, Has.Count.EqualTo(2));
                      Assert.That(storedItems.Select(item => item.ChargedPriceCents), Is.All.EqualTo(350));
                      Assert.That(storedItems.Select(item => item.SettledAtUtc), Is.All.Not.Null);
                      Assert.That(nextOrderNumber, Is.EqualTo(2));
                    });
  }

  private OrderAcceptanceRequest BuildRequest(SeededDomain seeded,
                                              Guid clientOrderId,
                                              OrderSettlementTerms? settlement = null)
  {
    return new()
           {
             ClientOrderId = clientOrderId,
             StaffMemberId = seeded.StaffMemberId,
             TableName = "Tisch 12",
             Note = null,
             Settlement = settlement,
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
