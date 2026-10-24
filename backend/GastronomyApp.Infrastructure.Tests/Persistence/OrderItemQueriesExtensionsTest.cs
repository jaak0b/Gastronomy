using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Persistence;

[TestFixture]
public sealed class OrderItemQueriesExtensionsTest
{
  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

  [Test]
  public async Task WhereNotHandedOut_AnItemHandedOutBesideAnOpenOne_KeepsTheOpenOne()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var stationOrderId = await PlaceOrderAsync(fixture, seeded, seeded.FestivalId);
    await AddItemAsync(fixture, seeded, stationOrderId, "Bratwurst", null, null);
    await AddItemAsync(fixture, seeded, stationOrderId, "Limonade", _now, null);
    await using var readContext = fixture.CreateContext();

    IReadOnlyList<string> names = await readContext.OrderItems.WhereNotHandedOut().Select(item => item.ItemName).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(names, Is.EqualTo(new[] { "Bratwurst" }));
  }

  [Test]
  public async Task WhereNotHandedOutOrNotSettled_ItemsAtEveryStep_KeepsEachThatStillOwesSomething()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var stationOrderId = await PlaceOrderAsync(fixture, seeded, seeded.FestivalId);
    await AddItemAsync(fixture, seeded, stationOrderId, "Bratwurst", null, null);
    await AddItemAsync(fixture, seeded, stationOrderId, "Limonade", _now, null);
    await AddItemAsync(fixture, seeded, stationOrderId, "Pommes", null, _now);
    await AddItemAsync(fixture, seeded, stationOrderId, "Bier", _now, _now);
    await using var readContext = fixture.CreateContext();

    IReadOnlyList<string> names = await readContext.OrderItems.WhereNotHandedOutOrNotSettled().Select(item => item.ItemName).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(names,
                Is.EquivalentTo(new[]
                                {
                                  "Bratwurst",
                                  "Limonade",
                                  "Pommes"
                                }));
  }

  [Test]
  public async Task WhereFestivalIs_ItemsOfTwoFestivals_KeepsThoseOfTheFestivalAskedFor()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var autumnFestivalId = await SeedAutumnFestivalAsync(fixture);
    await AddItemAsync(fixture, seeded, await PlaceOrderAsync(fixture, seeded, seeded.FestivalId), "Bratwurst", null, null);
    await AddItemAsync(fixture, seeded, await PlaceOrderAsync(fixture, seeded, autumnFestivalId), "Kürbissuppe", null, null);
    await using var readContext = fixture.CreateContext();

    IReadOnlyList<string> names = await readContext.OrderItems.WhereFestivalIs(seeded.FestivalId).Select(item => item.ItemName).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(names, Is.EqualTo(new[] { "Bratwurst" }));
  }

  [Test]
  public void EveryFilter_NoItems_ThrowsArgumentNullException()
  {
    Assert.Multiple(() =>
                    {
                      Assert.That(() => OrderItemQueriesExtensions.WhereNotHandedOut(null!), Throws.ArgumentNullException);
                      Assert.That(() => OrderItemQueriesExtensions.WhereNotHandedOutOrNotSettled(null!), Throws.ArgumentNullException);
                      Assert.That(() => OrderItemQueriesExtensions.WhereFestivalIs(null!, Guid.NewGuid()), Throws.ArgumentNullException);
                    });
  }

  private async Task<Guid> SeedAutumnFestivalAsync(SqliteInMemoryFixture fixture)
  {
    var festivalId = Guid.NewGuid();

    fixture.DbContext.Festivals.Add(new()
                                    {
                                      Id = festivalId,
                                      Name = "Herbstfest",
                                      StartsAtUtc = _now.AddDays(-60),
                                      EndsAtUtc = _now.AddDays(-59),
                                      NextOrderNumber = 1,
                                      IsHidden = false
                                    });
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return festivalId;
  }

  private async Task<Guid> PlaceOrderAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, Guid festivalId)
  {
    var tableId = await new DomainSeeder().SeedTableAsync(fixture.DbContext, festivalId, "Tisch 12", TestContext.CurrentContext.CancellationToken);
    var orderId = Guid.NewGuid();
    var stationOrderId = Guid.NewGuid();

    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = festivalId,
                    GlobalOrderNumber = 1,
                    StaffMemberId = seeded.StaffMemberId,
                    TableId = tableId,
                    CreatedAtUtc = _now
                  };

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = orderId,
                                  FestivalId = festivalId,
                                  StationId = seeded.KitchenStationId,
                                  StationOrderNumber = 1,
                                  DeliveryMode = DeliveryMode.Together
                                };

    order.StationOrders.Add(stationOrder);
    fixture.DbContext.Orders.Add(order);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return stationOrderId;
  }

  private async Task AddItemAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, Guid stationOrderId, string itemName, DateTime? handedOutAtUtc, DateTime? settledAtUtc)
  {
    fixture.DbContext.OrderItems.Add(new()
                                     {
                                       Id = Guid.NewGuid(),
                                       StationOrderId = stationOrderId,
                                       CatalogItemId = seeded.SausageItemId,
                                       ItemName = itemName,
                                       UnitPriceCents = 350,
                                       FulfilledAtUtc = handedOutAtUtc,
                                       SettledAtUtc = settledAtUtc
                                     });
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
