using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Queries;

[TestFixture]
public sealed class OrderItemQueriesTest
{
  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly OrderItemLifecycle _lifecycle = new();
  private readonly DateTime _unsavedPlacedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly DateTime _unsavedHandedOutAtUtc = new(2026, 9, 5, 19, 20, 0, DateTimeKind.Utc);
  private readonly Guid _kitchenId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

  [Test]
  public async Task WhereOpen_AnItemHandedOutAndOneCancelledBesideAnOpenOne_KeepsTheOpenOne()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var stationOrderId = await PlaceOrderAsync(fixture, seeded, seeded.FestivalId);
    await AddItemAsync(fixture, seeded, stationOrderId, "Bratwurst", null, null);
    await AddItemAsync(fixture, seeded, stationOrderId, "Limonade", _now, null);
    await AddCancelledItemAsync(fixture, seeded, stationOrderId, "Pommes", null, null);
    await using var readContext = fixture.CreateContext();

    IReadOnlyList<string> names = await readContext.OrderItems.WhereOpen().Select(item => item.ItemName).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(names, Is.EqualTo(new[] { "Bratwurst" }));
  }

  [Test]
  public async Task WhereOpen_AnItemPutBackAfterItWasHandedOut_KeepsIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var stationOrderId = await PlaceOrderAsync(fixture, seeded, seeded.FestivalId);
    await AddItemAsync(fixture, seeded, stationOrderId, "Bratwurst", _now, null);
    var item = await fixture.DbContext.OrderItems.SingleAsync(TestContext.CurrentContext.CancellationToken);
    new OrderItemLifecycle().AppendStatus(item, OrderItemState.Open, new(null, seeded.KitchenStationId), _now.AddMinutes(1));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await using var readContext = fixture.CreateContext();

    IReadOnlyList<string> names = await readContext.OrderItems.WhereOpen().Select(candidate => candidate.ItemName).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(names, Is.EqualTo(new[] { "Bratwurst" }));
  }

  [Test]
  public async Task WhereNotCancelled_AnOpenAHandedOutACancelledAndAnAcknowledgedItem_KeepsTheOpenAndTheHandedOutOne()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var stationOrderId = await PlaceOrderAsync(fixture, seeded, seeded.FestivalId);
    await AddItemAsync(fixture, seeded, stationOrderId, "Bratwurst", null, null);
    await AddItemAsync(fixture, seeded, stationOrderId, "Limonade", _now, null);
    await AddCancelledItemAsync(fixture, seeded, stationOrderId, "Pommes", _now, null);
    var acknowledged = await AddCancelledItemAsync(fixture, seeded, stationOrderId, "Bier", null, null);
    new OrderItemStatusSeeder().AcknowledgeCancellation(acknowledged, seeded.KitchenStationId, _now.AddMinutes(3));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await using var readContext = fixture.CreateContext();

    IReadOnlyList<string> names = await readContext.OrderItems.WhereNotCancelled().Select(item => item.ItemName).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(names,
                Is.EquivalentTo(new[]
                                {
                                  "Bratwurst",
                                  "Limonade"
                                }));
  }

  [Test]
  public async Task WhereToHandOutOrToSettle_ItemsAtEveryStep_KeepsEachThatStillOwesSomethingAndNoCancelledOne()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var stationOrderId = await PlaceOrderAsync(fixture, seeded, seeded.FestivalId);
    await AddItemAsync(fixture, seeded, stationOrderId, "Bratwurst", null, null);
    await AddItemAsync(fixture, seeded, stationOrderId, "Limonade", _now, null);
    await AddItemAsync(fixture, seeded, stationOrderId, "Pommes", null, _now);
    await AddItemAsync(fixture, seeded, stationOrderId, "Bier", _now, _now);
    await AddCancelledItemAsync(fixture, seeded, stationOrderId, "Wasser", null, null);
    await AddCancelledItemAsync(fixture, seeded, stationOrderId, "Cola", _now, null);
    await using var readContext = fixture.CreateContext();

    IReadOnlyList<string> names = await readContext.OrderItems.WhereToHandOutOrToSettle().Select(item => item.ItemName).ToListAsync(TestContext.CurrentContext.CancellationToken);

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
                      Assert.That(() => OrderItemQueries.WhereOpen(null!), Throws.ArgumentNullException);
                      Assert.That(() => OrderItemQueries.WhereNotCancelled(null!), Throws.ArgumentNullException);
                      Assert.That(() => OrderItemQueries.WhereToHandOutOrToSettle(null!), Throws.ArgumentNullException);
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

  [Test]
  public async Task ConsumedIngredients_ACancelledItemBesideAnOpenAndAHandedOutOne_ListsNoRowForTheCancelledOne()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var stationOrderId = await PlaceOrderAsync(fixture, seeded, seeded.FestivalId);
    await AddItemAsync(fixture, seeded, stationOrderId, "Open", null, null);
    await AddItemAsync(fixture, seeded, stationOrderId, "HandedOut", _now, null);
    await AddCancelledItemAsync(fixture, seeded, stationOrderId, "Cancelled", null, null);
    var unit = new Unit
               {
                 Id = Guid.NewGuid(),
                 Name = "Portion",
                 IsActive = true
               };
    Ingredient sausage = new()
                         {
                           Id = Guid.NewGuid(),
                           Name = "Wurst",
                           UnitId = unit.Id,
                           Unit = unit,
                           IsActive = true
                         };
    fixture.DbContext.Ingredients.Add(sausage);
    fixture.DbContext.CatalogItemIngredients.Add(new()
                                                 {
                                                   Id = Guid.NewGuid(),
                                                   CatalogItemId = seeded.SausageItemId,
                                                   IngredientId = sausage.Id,
                                                   Amount = 1
                                                 });
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await using var readContext = fixture.CreateContext();

    IReadOnlyList<ConsumedIngredient> consumed = await readContext.OrderItems.ConsumedIngredients(readContext.CatalogItemIngredients).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(consumed.Sum(row => row.Amount), Is.EqualTo(2));
  }

  [Test]
  public async Task IsOnTheQueueCard_InAWhereAndInMemory_KeepsOpenHandedOutAndAwaitingItemsAndDropsTheOtherCancelledOnes()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var stationOrderId = await PlaceOrderAsync(fixture, seeded, seeded.FestivalId);
    await AddItemAsync(fixture, seeded, stationOrderId, "Open", null, null);
    await AddItemAsync(fixture, seeded, stationOrderId, "HandedOut", _now, null);
    await AddCancelledItemAsync(fixture, seeded, stationOrderId, "CancelledWhileOpen", null, null);
    await AddCancelledItemAsync(fixture, seeded, stationOrderId, "CancelledAfterHandOut", _now, null);
    var acknowledged = await AddCancelledItemAsync(fixture, seeded, stationOrderId, "Acknowledged", null, null);
    new OrderItemStatusSeeder().AcknowledgeCancellation(acknowledged, seeded.KitchenStationId, _now.AddMinutes(3));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await using var readContext = fixture.CreateContext();
    var cancellationToken = TestContext.CurrentContext.CancellationToken;
    string[] onTheCard =
    [
      "Open",
      "HandedOut",
      "CancelledWhileOpen"
    ];

    IReadOnlyList<string> translated = await readContext.OrderItems.Where(item => item.IsOnTheQueueCard()).Select(item => item.ItemName).ToListAsync(cancellationToken);
    List<OrderItem> loaded = await readContext.OrderItems.AsNoTracking().Include(item => item.CurrentStatus!.PreviousStatus).ToListAsync(cancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(translated, Is.EquivalentTo(onTheCard));
                      Assert.That(loaded.Where(item => item.IsOnTheQueueCard()).Select(item => item.ItemName), Is.EquivalentTo(onTheCard));
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

  private async Task<OrderItem> AddCancelledItemAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, Guid stationOrderId, string itemName, DateTime? handedOutAtUtc, DateTime? settledAtUtc)
  {
    var item = await AddItemAsync(fixture, seeded, stationOrderId, itemName, handedOutAtUtc, settledAtUtc);
    new OrderItemStatusSeeder().Cancel(item, seeded.StaffMemberId, _now.AddMinutes(2));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return item;
  }

  private async Task<OrderItem> AddItemAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, Guid stationOrderId, string itemName, DateTime? handedOutAtUtc, DateTime? settledAtUtc)
  {
    OrderItem item = new()
                     {
                       Id = Guid.NewGuid(),
                       StationOrderId = stationOrderId,
                       CatalogItemId = seeded.SausageItemId,
                       ItemName = itemName,
                       UnitPriceCents = 350,
                       SettledAtUtc = settledAtUtc
                     };

    OrderItemStatusSeeder statusSeeder = new();
    fixture.DbContext.OrderItems.Add(statusSeeder.OpenAndHandOutOnSaveWhenAHandOutTimeIsGiven(item, seeded.StaffMemberId, DateTime.UtcNow, seeded.KitchenStationId, handedOutAtUtc));
    await statusSeeder.SaveAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    return item;
  }

  [Test]
  public void IsHandedOut_AnOpenAndAHandedOutItem_TellsThemApart()
  {
    var open = OpenUnsavedItem();
    var handedOut = OpenUnsavedItem();
    _lifecycle.AppendStatus(handedOut, OrderItemState.HandedOut, new(null, _kitchenId), _unsavedHandedOutAtUtc);

    Assert.Multiple(() =>
                    {
                      Assert.That(open.IsHandedOut(), Is.False);
                      Assert.That(handedOut.IsHandedOut(), Is.True);
                    });
  }

  [Test]
  public void StateSetAtUtc_AHandedOutItem_IsTheTimeOfItsCurrentStatus()
  {
    var item = HandedOutUnsavedItem();

    Assert.Multiple(() =>
                    {
                      Assert.That(item.State(), Is.EqualTo(OrderItemState.HandedOut));
                      Assert.That(item.StateSetAtUtc(), Is.EqualTo(_unsavedHandedOutAtUtc));
                    });
  }

  [Test]
  public void StateSetAtUtc_AnItemPutBack_IsTheTimeItWasPutBack()
  {
    var item = HandedOutUnsavedItem();
    _lifecycle.AppendStatus(item, OrderItemState.Open, new(null, _kitchenId), _unsavedHandedOutAtUtc.AddMinutes(1));

    Assert.Multiple(() =>
                    {
                      Assert.That(item.State(), Is.EqualTo(OrderItemState.Open));
                      Assert.That(item.StateSetAtUtc(), Is.EqualTo(_unsavedHandedOutAtUtc.AddMinutes(1)));
                    });
  }

  [Test]
  public void AwaitsCancellationAcknowledgement_ItemsCancelledWhileOpenAndAfterHandOut_OnlyTheOneCancelledWhileOpenWaits()
  {
    Assert.Multiple(() =>
                    {
                      Assert.That(Cancelled(OpenUnsavedItem()).AwaitsCancellationAcknowledgement(), Is.True);
                      Assert.That(Cancelled(HandedOutUnsavedItem()).AwaitsCancellationAcknowledgement(), Is.False);
                      Assert.That(Acknowledged(Cancelled(OpenUnsavedItem())).AwaitsCancellationAcknowledgement(), Is.False);
                      Assert.That(OpenUnsavedItem().AwaitsCancellationAcknowledgement(), Is.False);
                    });
  }

  [Test]
  public void IsCancelled_EveryState_IsTrueForBothCancelledStatesOnly()
  {
    Assert.Multiple(() =>
                    {
                      Assert.That(OpenUnsavedItem().IsCancelled(), Is.False);
                      Assert.That(HandedOutUnsavedItem().IsCancelled(), Is.False);
                      Assert.That(Cancelled(OpenUnsavedItem()).IsCancelled(), Is.True);
                      Assert.That(Acknowledged(Cancelled(OpenUnsavedItem())).IsCancelled(), Is.True);
                    });
  }

  [Test]
  public async Task EveryFilter_StoredItemsAtEveryStep_KeepsExactlyTheItemsTheInMemoryReadsName()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var stationOrderId = await PlaceOrderAsync(fixture, seeded, seeded.FestivalId);
    await AddItemAsync(fixture, seeded, stationOrderId, "Open", null, null);
    await AddItemAsync(fixture, seeded, stationOrderId, "HandedOut", _now, null);
    await AddItemAsync(fixture, seeded, stationOrderId, "OpenSettled", null, _now);
    await AddItemAsync(fixture, seeded, stationOrderId, "HandedOutSettled", _now, _now);
    await AddCancelledItemAsync(fixture, seeded, stationOrderId, "CancelledWhileOpen", null, null);
    await AddCancelledItemAsync(fixture, seeded, stationOrderId, "CancelledAfterHandOut", _now, null);
    await AddCancelledItemAsync(fixture, seeded, stationOrderId, "CancelledSettled", _now, _now);
    var acknowledged = await AddCancelledItemAsync(fixture, seeded, stationOrderId, "Acknowledged", null, null);
    new OrderItemStatusSeeder().AcknowledgeCancellation(acknowledged, seeded.KitchenStationId, _now.AddMinutes(3));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await using var readContext = fixture.CreateContext();
    var cancellationToken = TestContext.CurrentContext.CancellationToken;
    List<OrderItem> loaded = await readContext.OrderItems.AsNoTracking().Include(item => item.CurrentStatus!.PreviousStatus).ToListAsync(cancellationToken);

    IReadOnlyList<string> open = await readContext.OrderItems.WhereOpen().Select(item => item.ItemName).ToListAsync(cancellationToken);
    IReadOnlyList<string> notCancelled = await readContext.OrderItems.WhereNotCancelled().Select(item => item.ItemName).ToListAsync(cancellationToken);
    IReadOnlyList<string> toHandOutOrToSettle = await readContext.OrderItems.WhereToHandOutOrToSettle().Select(item => item.ItemName).ToListAsync(cancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(open, Is.EquivalentTo(loaded.Where(item => item.IsOpen()).Select(item => item.ItemName)));
                      Assert.That(notCancelled, Is.EquivalentTo(loaded.Where(item => !item.IsCancelled()).Select(item => item.ItemName)));
                      Assert.That(toHandOutOrToSettle,
                                  Is.EquivalentTo(new[]
                                                  {
                                                    "Open",
                                                    "HandedOut",
                                                    "OpenSettled"
                                                  }));
                      Assert.That(loaded.Where(item => item.IsOpenOrAwaitsCancellationAcknowledgement()).Select(item => item.ItemName),
                                  Is.EquivalentTo(new[]
                                                  {
                                                    "Open",
                                                    "OpenSettled",
                                                    "CancelledWhileOpen"
                                                  }));
                    });
  }

  [Test]
  public async Task AwaitsCancellationAcknowledgement_InATopLevelWhere_IsTranslatedAndKeepsOnlyTheItemCancelledWhileOpen()
  {
    using SqliteInMemoryFixture fixture = new();
    var stationOrderId = await SeedItemsCancelledAtEachStepAsync(fixture);
    await using var readContext = fixture.CreateContext();

    IReadOnlyList<string> names = await readContext.OrderItems.Where(item => item.StationOrderId == stationOrderId && item.AwaitsCancellationAcknowledgement()).Select(item => item.ItemName).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(names, Is.EqualTo(new[] { "CancelledWhileOpen" }));
  }

  [Test]
  public async Task AwaitsCancellationAcknowledgement_NestedInAnAny_IsTranslatedAndTellsTheStationOrdersApart()
  {
    using SqliteInMemoryFixture fixture = new();
    await SeedItemsCancelledAtEachStepAsync(fixture);
    await using var readContext = fixture.CreateContext();
    var cancellationToken = TestContext.CurrentContext.CancellationToken;

    var withAnItemAwaitingAcknowledgement = await readContext.StationOrders.CountAsync(stationOrder => stationOrder.Items.Any(item => item.AwaitsCancellationAcknowledgement()), cancellationToken);
    var withAnItemHandedOut = await readContext.StationOrders.CountAsync(stationOrder => stationOrder.Items.Any(item => item.IsHandedOut()), cancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(withAnItemAwaitingAcknowledgement, Is.EqualTo(1));
                      Assert.That(withAnItemHandedOut, Is.Zero);
                    });
  }

  [Test]
  public async Task StateAndChecks_InAProjection_AreTranslatedIntoTheSqlAndReadEachItemsStatus()
  {
    using SqliteInMemoryFixture fixture = new();
    await SeedItemsCancelledAtEachStepAsync(fixture);
    await using var readContext = fixture.CreateContext();
    var projection = readContext.OrderItems.Select(item => new
                                                           {
                                                             item.ItemName,
                                                             State = item.State(),
                                                             IsCancelled = item.IsCancelled(),
                                                             AwaitsAcknowledgement = item.AwaitsCancellationAcknowledgement()
                                                           });
    var cancelledCounts = readContext.StationOrders.Select(stationOrder => stationOrder.Items.Count(item => item.IsCancelled()));

    var rows = await projection.ToListAsync(TestContext.CurrentContext.CancellationToken);
    var cancelledCount = await cancelledCounts.SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(projection.ToQueryString(), Does.Contain("\"OrderItemStatuses\""));
                      Assert.That(rows.Single(row => row.ItemName == "Open").State, Is.EqualTo(OrderItemState.Open));
                      Assert.That(rows.Single(row => row.ItemName == "Open").IsCancelled, Is.False);
                      Assert.That(rows.Single(row => row.ItemName == "CancelledWhileOpen").AwaitsAcknowledgement, Is.True);
                      Assert.That(rows.Single(row => row.ItemName == "CancelledAfterHandOut").State, Is.EqualTo(OrderItemState.Cancelled));
                      Assert.That(rows.Single(row => row.ItemName == "CancelledAfterHandOut").AwaitsAcknowledgement, Is.False);
                      Assert.That(cancelledCount, Is.EqualTo(2));
                    });
  }

  private async Task<Guid> SeedItemsCancelledAtEachStepAsync(SqliteInMemoryFixture fixture)
  {
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var stationOrderId = await PlaceOrderAsync(fixture, seeded, seeded.FestivalId);
    await AddItemAsync(fixture, seeded, stationOrderId, "Open", null, null);
    await AddCancelledItemAsync(fixture, seeded, stationOrderId, "CancelledWhileOpen", null, null);
    await AddCancelledItemAsync(fixture, seeded, stationOrderId, "CancelledAfterHandOut", _now, null);

    return stationOrderId;
  }

  private OrderItem HandedOutUnsavedItem()
  {
    var item = OpenUnsavedItem();
    _lifecycle.AppendStatus(item, OrderItemState.HandedOut, new(null, _kitchenId), _unsavedHandedOutAtUtc);

    return item;
  }

  private OrderItem Cancelled(OrderItem item)
  {
    _lifecycle.AppendStatus(item, OrderItemState.Cancelled, new(Guid.NewGuid(), null), _unsavedHandedOutAtUtc.AddMinutes(10));

    return item;
  }

  private OrderItem Acknowledged(OrderItem item)
  {
    _lifecycle.AppendStatus(item, OrderItemState.CancellationAcknowledged, new(null, _kitchenId), _unsavedHandedOutAtUtc.AddMinutes(11));

    return item;
  }

  private OrderItem NewUnsavedItem()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = Guid.NewGuid(),
             CatalogItemId = Guid.NewGuid(),
             ItemName = "Bratwurst",
             UnitPriceCents = 350
           };
  }

  private OrderItem OpenUnsavedItem()
  {
    var item = NewUnsavedItem();
    _lifecycle.AppendStatus(item, OrderItemState.Open, new(Guid.NewGuid(), null), _unsavedPlacedAtUtc);
    _lifecycle.PointAtFirstStatus(item);

    return item;
  }
}
