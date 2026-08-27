using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class OrderAcceptanceTransactionTest
{
    [Test]
    public async Task AcceptAsync_NewClientOrderId_InsertsOrderTicketsAndLines()
    {
        using SqliteInMemoryFixture fixture = new();
        SeededDomain seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
        OrderAcceptanceTransaction transaction = new OrderAcceptanceComposition().Create(fixture.DbContext);

        Result<OrderAcceptanceResult, OrderValidationFailure> result = await transaction.AcceptAsync(
            BuildRequest(seeded, Guid.NewGuid()),
            TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.WasAlreadyAccepted, Is.False);
            Assert.That(result.Value.Order.GlobalOrderNumber, Is.EqualTo(1));
            Assert.That(result.Value.Order.Tickets, Has.Count.EqualTo(2));
            Assert.That(result.Value.Order.Lines, Has.Count.EqualTo(2));
            Assert.That(result.Value.Order.TotalCents, Is.EqualTo(950));
        });
    }

    [Test]
    public async Task AcceptAsync_RepeatClientOrderId_ReturnsExistingOrderAndInsertsNothing()
    {
        using SqliteInMemoryFixture fixture = new();
        SeededDomain seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
        OrderAcceptanceTransaction transaction = new OrderAcceptanceComposition().Create(fixture.DbContext);
        Guid clientOrderId = Guid.NewGuid();

        Result<OrderAcceptanceResult, OrderValidationFailure> first = await transaction.AcceptAsync(
            BuildRequest(seeded, clientOrderId), TestContext.CurrentContext.CancellationToken);
        Result<OrderAcceptanceResult, OrderValidationFailure> second = await transaction.AcceptAsync(
            BuildRequest(seeded, clientOrderId), TestContext.CurrentContext.CancellationToken);

        int orderCount = await fixture.DbContext.Orders.CountAsync(TestContext.CurrentContext.CancellationToken);

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
        GastronomyAppDbContext seedContext = fixture.CreateContext();
        SeededDomain seeded = await new DomainSeeder().SeedAsync(seedContext, TestContext.CurrentContext.CancellationToken);
        Guid clientOrderId = Guid.NewGuid();

        OrderAcceptanceComposition composition = new();
        OrderAcceptanceTransaction firstTransaction = composition.Create(fixture.CreateContext());
        OrderAcceptanceTransaction secondTransaction = composition.Create(fixture.CreateContext());

        Task<Result<OrderAcceptanceResult, OrderValidationFailure>> firstCall =
            Task.Run(() => firstTransaction.AcceptAsync(BuildRequest(seeded, clientOrderId), TestContext.CurrentContext.CancellationToken));
        Task<Result<OrderAcceptanceResult, OrderValidationFailure>> secondCall =
            Task.Run(() => secondTransaction.AcceptAsync(BuildRequest(seeded, clientOrderId), TestContext.CurrentContext.CancellationToken));

        Result<OrderAcceptanceResult, OrderValidationFailure>[] results = await Task.WhenAll(firstCall, secondCall);

        GastronomyAppDbContext verificationContext = fixture.CreateContext();
        int orderCount = await verificationContext.Orders.CountAsync(TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(results[0].IsSuccess, Is.True);
            Assert.That(results[1].IsSuccess, Is.True);
            Assert.That(results[1].Value.Order.Id, Is.EqualTo(results[0].Value.Order.Id));
            Assert.That(
                results.Count(result => result.Value.WasAlreadyAccepted),
                Is.EqualTo(1));
            Assert.That(orderCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AcceptAsync_MultipleLocations_AllocatesIndependentSequenceNumbers()
    {
        using SqliteInMemoryFixture fixture = new();
        SeededDomain seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
        OrderAcceptanceTransaction transaction = new OrderAcceptanceComposition().Create(fixture.DbContext);

        await transaction.AcceptAsync(BuildRequest(seeded, Guid.NewGuid()), TestContext.CurrentContext.CancellationToken);
        Result<OrderAcceptanceResult, OrderValidationFailure> second = await transaction.AcceptAsync(
            BuildRequest(seeded, Guid.NewGuid()), TestContext.CurrentContext.CancellationToken);

        LocationTicket kitchenTicket = second.Value.Order.Tickets.Single(ticket => ticket.ProductionLocationId == seeded.KitchenLocationId);
        LocationTicket barTicket = second.Value.Order.Tickets.Single(ticket => ticket.ProductionLocationId == seeded.BarLocationId);

        Assert.Multiple(() =>
        {
            Assert.That(kitchenTicket.LocationSequenceNumber, Is.EqualTo(2));
            Assert.That(barTicket.LocationSequenceNumber, Is.EqualTo(2));
            Assert.That(second.Value.Order.GlobalOrderNumber, Is.EqualTo(2));
        });
    }

    private OrderAcceptanceRequest BuildRequest(SeededDomain seeded, Guid clientOrderId)
    {
        return new OrderAcceptanceRequest
        {
            ClientOrderId = clientOrderId,
            EventSessionId = seeded.EventSessionId,
            ServerPersonId = seeded.ServerPersonId,
            DeviceId = seeded.DeviceId,
            TableLabel = "Tisch 12",
            Note = null,
            Lines =
            [
                new OrderAcceptanceLineRequest { CatalogItemId = seeded.SausageItemId, Quantity = 2, Note = null },
                new OrderAcceptanceLineRequest { CatalogItemId = seeded.LemonadeItemId, Quantity = 1, Note = null },
            ],
        };
    }
}
