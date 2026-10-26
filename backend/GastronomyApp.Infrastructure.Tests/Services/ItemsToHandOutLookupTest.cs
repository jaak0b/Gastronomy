using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class ItemsToHandOutLookupTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _clock = new(DateTimeOffset.UtcNow);
    _lookup = new(_fixture.DbContext, new(_fixture.DbContext, new(), _clock));
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private readonly OrderItemStatusSeeder _statusSeeder = new();

  private FakeTimeProvider _clock = null!;
  private SqliteInMemoryFixture _fixture = null!;
  private ItemsToHandOutLookup _lookup = null!;
  private SeededDomain _seeded = null!;

  [Test]
  public async Task FindStationIdsIfRunningAsync_AFestivalThatRunsAndAStationWithAnItemNotYetHandedOut_NamesThatStationOnly()
  {
    await PlaceKitchenOrderAsync(_seeded.FestivalId);

    IReadOnlyCollection<Guid> stationIds = await _lookup.FindStationIdsIfRunningAsync(await FestivalAsync(_seeded.FestivalId), TestContext.CurrentContext.CancellationToken);

    Assert.That(stationIds, Is.EqualTo(new[] { _seeded.KitchenStationId }));
  }

  [Test]
  public async Task FindStationIdsIfRunningAsync_AStationThatHandedOutEverything_NamesNoStation()
  {
    await HandOutAsync(await PlaceKitchenOrderAsync(_seeded.FestivalId));

    IReadOnlyCollection<Guid> stationIds = await _lookup.FindStationIdsIfRunningAsync(await FestivalAsync(_seeded.FestivalId), TestContext.CurrentContext.CancellationToken);

    Assert.That(stationIds, Is.Empty);
  }

  [Test]
  public async Task FindStationIdsIfRunningAsync_AnItemNotYetHandedOutAtAnotherFestival_NamesNoStation()
  {
    var autumnFestivalId = await SeedAutumnFestivalAsync();
    await PlaceKitchenOrderAsync(autumnFestivalId);

    IReadOnlyCollection<Guid> stationIds = await _lookup.FindStationIdsIfRunningAsync(await FestivalAsync(_seeded.FestivalId), TestContext.CurrentContext.CancellationToken);

    Assert.That(stationIds, Is.Empty);
  }

  [Test]
  public async Task FindStationIdsIfRunningAsync_AFestivalThatIsOver_NamesNoStationEvenWithItemsLeft()
  {
    await PlaceKitchenOrderAsync(_seeded.FestivalId);
    var festival = await FestivalAsync(_seeded.FestivalId);
    festival.StartsAtUtc = DateTime.UtcNow.AddDays(-2);
    festival.EndsAtUtc = DateTime.UtcNow.AddHours(-1);

    IReadOnlyCollection<Guid> stationIds = await _lookup.FindStationIdsIfRunningAsync(festival, TestContext.CurrentContext.CancellationToken);

    Assert.That(stationIds, Is.Empty);
  }

  [Test]
  public async Task FindStationIdsIfRunningAsync_AFestivalThatHasNotStarted_NamesNoStation()
  {
    await PlaceKitchenOrderAsync(_seeded.FestivalId);
    var festival = await FestivalAsync(_seeded.FestivalId);
    festival.StartsAtUtc = DateTime.UtcNow.AddHours(1);
    festival.EndsAtUtc = DateTime.UtcNow.AddHours(6);

    IReadOnlyCollection<Guid> stationIds = await _lookup.FindStationIdsIfRunningAsync(festival, TestContext.CurrentContext.CancellationToken);

    Assert.That(stationIds, Is.Empty);
  }

  [Test]
  public async Task FindStationIdsAtTheRunningFestivalAsync_AFestivalRuns_NamesItsStationsWithItemsToHandOut()
  {
    await PlaceKitchenOrderAsync(_seeded.FestivalId);

    IReadOnlyCollection<Guid> stationIds = await _lookup.FindStationIdsAtTheRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(stationIds, Is.EqualTo(new[] { _seeded.KitchenStationId }));
  }

  [Test]
  public async Task FindStationIdsAtTheRunningFestivalAsync_NoFestivalRuns_NamesNoStation()
  {
    await PlaceKitchenOrderAsync(_seeded.FestivalId);
    var festival = await FestivalAsync(_seeded.FestivalId);
    festival.StartsAtUtc = DateTime.UtcNow.AddDays(-2);
    festival.EndsAtUtc = DateTime.UtcNow.AddHours(-1);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    IReadOnlyCollection<Guid> stationIds = await _lookup.FindStationIdsAtTheRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(stationIds, Is.Empty);
  }

  [Test]
  public void FindStationIdsIfRunningAsync_NoFestival_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _lookup.FindStationIdsIfRunningAsync(null!, TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  private async Task<Festival> FestivalAsync(Guid festivalId)
  {
    return await _fixture.DbContext.Festivals.SingleAsync(festival => festival.Id == festivalId, TestContext.CurrentContext.CancellationToken);
  }

  private async Task<Guid> SeedAutumnFestivalAsync()
  {
    var festivalId = Guid.NewGuid();

    _fixture.DbContext.Festivals.Add(new()
                                     {
                                       Id = festivalId,
                                       Name = "Herbstfest",
                                       StartsAtUtc = DateTime.UtcNow.AddDays(-60),
                                       EndsAtUtc = DateTime.UtcNow.AddDays(-59),
                                       NextOrderNumber = 1,
                                       IsHidden = false
                                     });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return festivalId;
  }

  private async Task HandOutAsync(IReadOnlyCollection<Guid> orderItemIds)
  {
    List<Guid> ids = orderItemIds.ToList();
    List<OrderItem> items = await _fixture.DbContext.OrderItems.Include(item => item.CurrentStatus).Include(item => item.StationOrder).Where(item => ids.Contains(item.Id)).ToListAsync(TestContext.CurrentContext.CancellationToken);

    foreach (var item in items)
      _statusSeeder.HandOut(item, item.StationOrder.StationId, DateTime.UtcNow);

    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<IReadOnlyList<Guid>> PlaceKitchenOrderAsync(Guid festivalId)
  {
    var tableId = await new DomainSeeder().SeedTableAsync(_fixture.DbContext, festivalId, "Tisch 12", TestContext.CurrentContext.CancellationToken);
    var orderId = Guid.NewGuid();
    var stationOrderId = Guid.NewGuid();

    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = festivalId,
                    GlobalOrderNumber = 1,
                    StaffMemberId = _seeded.StaffMemberId,
                    TableId = tableId,
                    CreatedAtUtc = DateTime.UtcNow
                  };

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = orderId,
                                  FestivalId = festivalId,
                                  StationId = _seeded.KitchenStationId,
                                  StationOrderNumber = 1,
                                  DeliveryMode = DeliveryMode.Together
                                };

    stationOrder.Items.Add(BuildItem(stationOrderId, _seeded.SausageItemId, "Bratwurst", 350));
    stationOrder.Items.Add(BuildItem(stationOrderId, _seeded.LemonadeItemId, "Limonade", 250));
    order.StationOrders.Add(stationOrder);

    _fixture.DbContext.Orders.Add(order);
    await _statusSeeder.SaveAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    return stationOrder.Items.Select(item => item.Id).ToList();
  }

  private OrderItem BuildItem(Guid stationOrderId, Guid catalogItemId, string itemName, int unitPriceCents)
  {
    OrderItem item = new()
                     {
                       Id = Guid.NewGuid(),
                       StationOrderId = stationOrderId,
                       CatalogItemId = catalogItemId,
                       ItemName = itemName,
                       UnitPriceCents = unitPriceCents
                     };

    return _statusSeeder.Open(item, _seeded.StaffMemberId, DateTime.UtcNow);
  }
}
