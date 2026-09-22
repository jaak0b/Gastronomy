using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class StationRepositoryTest
{
  [SetUp]
  public void SetUp()
  {
    _clock = new(new(_now));
  }

  private readonly StationService _stationService = new();
  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

  private FakeTimeProvider _clock = null!;

  [Test]
  public async Task FindAtFestivalAsync_TheStationsOfOneFestival_ReturnsThemByTheirPlaceInTheList()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext, _clock, new());

    IReadOnlyCollection<Station> stations = await repository.FindAtFestivalAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stations.Select(station => station.Id),
                Is.EqualTo(new[]
                           {
                             seeded.KitchenStationId,
                             seeded.BarStationId
                           }));
  }

  [Test]
  public async Task FindAllAsync_WithoutAFestival_SaysNoStationBelongsToOne()
  {
    using SqliteInMemoryFixture fixture = new();
    await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext, _clock, new());

    IReadOnlyList<Station> stations = await repository.FindAllAsync(null, TestContext.CurrentContext.CancellationToken);

    Assert.That(stations.Select(station => _stationService.IsAtTheFestival(station)), Is.All.False);
  }

  [Test]
  public async Task FindAllAsync_AFestivalTheStationTakesPartIn_SaysTheStationBelongsToIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext, _clock, new());

    IReadOnlyList<Station> stations = await repository.FindAllAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stations.Select(station => _stationService.IsAtTheFestival(station)), Is.All.True);
  }

  [Test]
  public async Task FindAllAsync_AStationHoldingATablet_CarriesWhenThatTabletWasLastSeen()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var lastSeenAtUtc = _now.AddMinutes(-3);
    await GiveTheKitchenATabletAsync(fixture, seeded, lastSeenAtUtc);

    StationRepository repository = new(fixture.DbContext, _clock, new());

    IReadOnlyList<Station> stations = await repository.FindAllAsync(null, TestContext.CurrentContext.CancellationToken);

    var kitchen = stations.First(station => station.Id == seeded.KitchenStationId);

    Assert.Multiple(() =>
                    {
                      Assert.That(kitchen.DeviceId, Is.EqualTo(seeded.DeviceId));
                      Assert.That(kitchen.Device!.LastSeenAtUtc, Is.EqualTo(lastSeenAtUtc));
                    });
  }

  [Test]
  public async Task FindAllAsync_AnInvitationWhoseTimeRanOut_NoLongerCountsAsOutstanding()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await InviteTheKitchenAsync(fixture, seeded, _now.AddMinutes(2));

    StationRepository repository = new(fixture.DbContext, _clock, new());

    IReadOnlyList<Station> stillValid = await repository.FindAllAsync(null, TestContext.CurrentContext.CancellationToken);

    _clock.Advance(TimeSpan.FromMinutes(5));

    IReadOnlyList<Station> afterItRanOut = await repository.FindAllAsync(null, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(_stationService.HasOutstandingInvitation(stillValid.First(station => station.Id == seeded.KitchenStationId)), Is.True);
                      Assert.That(_stationService.HasOutstandingInvitation(afterItRanOut.First(station => station.Id == seeded.KitchenStationId)), Is.False);
                    });
  }

  [Test]
  public async Task ExistsAsync_AStationNobodyEverCreated_AnswersFalse()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext, _clock, new());

    Assert.Multiple(async () =>
                    {
                      Assert.That(await repository.ExistsAsync(seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken), Is.True);
                      Assert.That(await repository.ExistsAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken), Is.False);
                    });
  }

  [Test]
  public async Task AddAsync_ANewStation_StoresItWhenTheChangesAreSaved()
  {
    using SqliteInMemoryFixture fixture = new();
    await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext, _clock, new());
    var stationId = Guid.NewGuid();

    await repository.AddAsync(new()
                              {
                                Id = stationId,
                                Name = "Kuchenbuffet",
                                SortOrder = 3,
                                IsActive = true
                              },
                              TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();

    Assert.That(await readContext.Stations.AnyAsync(station => station.Id == stationId, TestContext.CurrentContext.CancellationToken), Is.True);
  }

  [Test]
  public async Task FindAtFestivalWithOpenItemsAsync_TheStationsOfOneFestival_ReturnsThemByTheirPlaceInTheList()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext, _clock, new());

    IReadOnlyList<Station> stations = await repository.FindAtFestivalWithOpenItemsAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stations.Select(station => station.Id),
                Is.EqualTo(new[]
                           {
                             seeded.KitchenStationId,
                             seeded.BarStationId
                           }));
  }

  [Test]
  public async Task FindAtFestivalWithOpenItemsAsync_OpenItems_CarryTheArticleTheyWereOrderedFrom()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await GiveTheSausageAProductionTimeAsync(fixture, seeded);
    await PlaceKitchenOrderAsync(fixture, seeded);

    StationRepository repository = new(fixture.DbContext, _clock, new());

    IReadOnlyList<Station> stations = await repository.FindAtFestivalWithOpenItemsAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stations[0].StationOrders.SelectMany(stationOrder => stationOrder.Items).Select(item => item.CatalogItem.ProductionMinutes),
                Is.EquivalentTo(new double?[]
                                {
                                  4,
                                  null
                                }));
  }

  [Test]
  public async Task FindAtFestivalWithOpenItemsAsync_AnItemTheStationHandedOut_LeavesItOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    IReadOnlyList<Guid> items = await PlaceKitchenOrderAsync(fixture, seeded);
    await HandOutAsync(fixture, items);

    StationRepository repository = new(fixture.DbContext, _clock, new());

    IReadOnlyList<Station> stations = await repository.FindAtFestivalWithOpenItemsAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stations.SelectMany(station => station.StationOrders).SelectMany(stationOrder => stationOrder.Items), Is.Empty);
  }

  private async Task GiveTheSausageAProductionTimeAsync(SqliteInMemoryFixture fixture, SeededDomain seeded)
  {
    var sausage = await fixture.DbContext.CatalogItems.FirstAsync(item => item.Id == seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);
    sausage.ProductionMinutes = 4;

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task HandOutAsync(SqliteInMemoryFixture fixture, IReadOnlyCollection<Guid> orderItemIds)
  {
    List<Guid> ids = orderItemIds.ToList();
    List<OrderItem> items = await fixture.DbContext.OrderItems.Where(item => ids.Contains(item.Id)).ToListAsync(TestContext.CurrentContext.CancellationToken);

    foreach (var item in items)
      item.FulfilledAtUtc = _now.AddMinutes(5);

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<IReadOnlyList<Guid>> PlaceKitchenOrderAsync(SqliteInMemoryFixture fixture, SeededDomain seeded)
  {
    var orderId = Guid.NewGuid();
    var stationOrderId = Guid.NewGuid();

    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = seeded.FestivalId,
                    GlobalOrderNumber = 1,
                    StaffMemberId = seeded.StaffMemberId,
                    TableName = "Tisch 12",
                    CreatedAtUtc = _now
                  };

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = orderId,
                                  FestivalId = seeded.FestivalId,
                                  StationId = seeded.KitchenStationId,
                                  StationOrderNumber = 1,
                                  DeliveryMode = DeliveryMode.Together
                                };

    stationOrder.Items.Add(BuildItem(stationOrderId, seeded.SausageItemId, "Bratwurst", 350));
    stationOrder.Items.Add(BuildItem(stationOrderId, seeded.LemonadeItemId, "Limonade", 250));
    order.StationOrders.Add(stationOrder);

    fixture.DbContext.Orders.Add(order);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return stationOrder.Items.Select(item => item.Id).ToList();
  }

  private OrderItem BuildItem(Guid stationOrderId, Guid catalogItemId, string itemName, int unitPriceCents)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = stationOrderId,
             CatalogItemId = catalogItemId,
             ItemName = itemName,
             UnitPriceCents = unitPriceCents
           };
  }

  private async Task GiveTheKitchenATabletAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, DateTime lastSeenAtUtc)
  {
    fixture.DbContext.Devices.Add(new()
                                  {
                                    Id = seeded.DeviceId,
                                    Language = "de",
                                    TokenHash = [1],
                                    TokenSalt = [2],
                                    TokenIterations = 1,
                                    TokenAlgorithm = "PBKDF2-HMAC-SHA512",
                                    TokenLookupId = Guid.NewGuid().ToString(),
                                    CreatedAtUtc = _now.AddHours(-1),
                                    LastSeenAtUtc = lastSeenAtUtc
                                  });

    var kitchen = await fixture.DbContext.Stations.FirstAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    kitchen.DeviceId = seeded.DeviceId;

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task InviteTheKitchenAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, DateTime expiresAtUtc)
  {
    var invitationId = Guid.NewGuid();

    fixture.DbContext.EnrolmentInvitations.Add(new()
                                               {
                                                 Id = invitationId,
                                                 QRCodeHash = [1],
                                                 QRCodeSalt = [2],
                                                 QRCodeIterations = 1,
                                                 QRCodeAlgorithm = "PBKDF2-HMAC-SHA512",
                                                 CreatedAtUtc = _now.AddMinutes(-1),
                                                 ExpiresAtUtc = expiresAtUtc,
                                                 ConsumedAtUtc = null,
                                                 ConsumedByDeviceId = null
                                               });

    var kitchen = await fixture.DbContext.Stations.FirstAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    kitchen.EnrolmentInvitationId = invitationId;

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
