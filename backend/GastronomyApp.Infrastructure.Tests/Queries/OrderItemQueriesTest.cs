using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Results;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Queries;

[TestFixture]
public sealed class OrderItemQueriesTest
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
                      Assert.That(() => OrderItemQueries.WhereNotHandedOut(null!), Throws.ArgumentNullException);
                      Assert.That(() => OrderItemQueries.WhereNotHandedOutOrNotSettled(null!), Throws.ArgumentNullException);
                      Assert.That(() => OrderItemQueries.WhereFestivalIs(null!, Guid.NewGuid()), Throws.ArgumentNullException);
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

  [Test]
  public async Task ConsumedIngredients_AnItemWithTwoRecipeLinesBesideOneWithout_ListsOneRowPerRecipeLineWithTheOrderTime()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var stationOrderId = await PlaceOrderAsync(fixture, seeded, seeded.FestivalId);
    await AddItemAsync(fixture, seeded, stationOrderId, "Bratwurst", null, null);
    var unit = new Unit
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
    Ingredient bun = new()
                     {
                       Id = Guid.NewGuid(),
                       Name = "Broetchen",
                       UnitId = unit.Id,
                       Unit = unit,
                       IsActive = true
                     };
    fixture.DbContext.Ingredients.AddRange(mustard, bun);
    fixture.DbContext.CatalogItemIngredients.AddRange(new CatalogItemIngredient
                                                      {
                                                        Id = Guid.NewGuid(),
                                                        CatalogItemId = seeded.SausageItemId,
                                                        IngredientId = mustard.Id,
                                                        Amount = 20
                                                      },
                                                      new CatalogItemIngredient
                                                      {
                                                        Id = Guid.NewGuid(),
                                                        CatalogItemId = seeded.SausageItemId,
                                                        IngredientId = bun.Id,
                                                        Amount = 1
                                                      });
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await using var readContext = fixture.CreateContext();

    IReadOnlyList<ConsumedIngredient> consumed = await readContext.OrderItems.ConsumedIngredients(readContext.CatalogItemIngredients).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(consumed.Select(row => row.IngredientId),
                                  Is.EquivalentTo(new[]
                                                  {
                                                    mustard.Id,
                                                    bun.Id
                                                  }));
                      Assert.That(consumed.Single(row => row.IngredientId == mustard.Id).Amount, Is.EqualTo(20));
                      Assert.That(consumed.Select(row => row.OrderedAtUtc), Is.All.EqualTo(_now));
                    });
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
