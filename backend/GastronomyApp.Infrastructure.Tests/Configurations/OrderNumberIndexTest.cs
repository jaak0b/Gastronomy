using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Configurations;

public sealed class OrderNumberIndexTest
{
  [Test]
  public async Task Save_ASecondOrderWithTheGlobalOrderNumberOfTheFirst_IsRefused()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    await using var first = fixture.CreateContext();
    first.Orders.Add(BuildOrder(seeded, 7, 1));
    await first.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var second = fixture.CreateContext();
    second.Orders.Add(BuildOrder(seeded, 7, 2));

    Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));
  }

  [Test]
  public async Task Save_ASecondStationOrderWithTheStationOrderNumberOfTheFirstAtTheSameStation_IsRefused()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    await using var first = fixture.CreateContext();
    first.Orders.Add(BuildOrderWithStationOrder(seeded, 1, 4));
    await first.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var second = fixture.CreateContext();
    second.Orders.Add(BuildOrderWithStationOrder(seeded, 2, 4));

    Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));
  }

  private Order BuildOrder(SeededDomain seeded, int globalOrderNumber, int distinguisher)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             ClientOrderId = Guid.NewGuid(),
             FestivalId = seeded.FestivalId,
             GlobalOrderNumber = globalOrderNumber,
             StaffMemberId = seeded.StaffMemberId,
             TableName = $"Tisch {distinguisher}",
             Note = null,
             CreatedAtUtc = new(2026, 8, 27, 18, 30, 0, DateTimeKind.Utc)
           };
  }

  private Order BuildOrderWithStationOrder(SeededDomain seeded, int globalOrderNumber, int stationOrderNumber)
  {
    var order = BuildOrder(seeded, globalOrderNumber, globalOrderNumber);

    order.StationOrders.Add(new()
                            {
                              Id = Guid.NewGuid(),
                              OrderId = order.Id,
                              FestivalId = seeded.FestivalId,
                              StationId = seeded.KitchenStationId,
                              StationOrderNumber = stationOrderNumber,
                              DeliveryMode = DeliveryMode.Together
                            });

    return order;
  }
}
