using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class OrderRepositoryTest
{
    [Test]
    public async Task AddAsync_NewOrder_PersistsOrderTicketsAndLines()
    {
        using SqliteInMemoryFixture fixture = new();
        SeededDomain seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
        OrderRepository repository = new(fixture.DbContext);
        Order order = BuildOrder(seeded, Guid.NewGuid(), 700);

        await repository.AddAsync(order, TestContext.CurrentContext.CancellationToken);

        Order? reloaded = await repository.FindByClientOrderIdAsync(order.ClientOrderId, TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded!.Tickets, Has.Count.EqualTo(1));
            Assert.That(reloaded.Lines, Has.Count.EqualTo(1));
            Assert.That(reloaded.Lines[0].ItemNameSnapshot, Is.EqualTo("Bratwurst"));
            Assert.That(reloaded.TotalCents, Is.EqualTo(700));
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

    [Test]
    public async Task AddAsync_TotalDisagreesWithLines_Throws()
    {
        using SqliteInMemoryFixture fixture = new();
        SeededDomain seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
        OrderRepository repository = new(fixture.DbContext);
        Order order = BuildOrder(seeded, Guid.NewGuid(), 1);

        Assert.That(
            async () => await repository.AddAsync(order, TestContext.CurrentContext.CancellationToken),
            Throws.InstanceOf<InvalidOperationException>());
    }

    private Order BuildOrder(SeededDomain seeded, Guid clientOrderId, int totalCents)
    {
        DateTime createdAtUtc = new(2026, 8, 27, 18, 30, 0, DateTimeKind.Utc);
        Guid orderId = Guid.NewGuid();
        Guid ticketId = Guid.NewGuid();

        Order order = new()
        {
            Id = orderId,
            EventSessionId = seeded.EventSessionId,
            ClientOrderId = clientOrderId,
            GlobalOrderNumber = 1,
            ServerPersonId = seeded.ServerPersonId,
            DeviceId = seeded.DeviceId,
            TableLabel = "Tisch 12",
            Note = null,
            TotalCents = totalCents,
            Status = OrderStatus.Accepted,
            CreatedAtUtc = createdAtUtc,
        };

        order.Tickets.Add(new LocationTicket
        {
            Id = ticketId,
            OrderId = orderId,
            ProductionLocationId = seeded.KitchenLocationId,
            LocationSequenceNumber = 1,
            Status = LocationTicketStatus.Queued,
            ReprintCount = 0,
            CreatedAtUtc = createdAtUtc,
        });

        order.Lines.Add(new OrderLine
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            LocationTicketId = ticketId,
            CatalogItemId = seeded.SausageItemId,
            ChosenProductionLocationId = null,
            ItemNameSnapshot = "Bratwurst",
            UnitPriceCentsSnapshot = 350,
            Quantity = 2,
            Note = null,
        });

        return order;
    }
}
