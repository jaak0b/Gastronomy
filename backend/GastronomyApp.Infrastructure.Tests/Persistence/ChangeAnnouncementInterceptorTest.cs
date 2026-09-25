using FakeItEasy;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Announcements;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Persistence;

[TestFixture]
public sealed class ChangeAnnouncementInterceptorTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _announcer = A.Fake<ICommittedChangeAnnouncer>();
    _afterCommitActions = new(_announcer);
    _afterCommitActions.StartCollecting();
    _interceptor = new(_afterCommitActions);
    _database = new(new DbContextOptionsBuilder<GastronomyAppDbContext>().UseSqlite(_fixture.Connection).AddInterceptors(_interceptor).Options);
  }

  [TearDown]
  public async Task TearDown()
  {
    await _database.DisposeAsync();
    _fixture.Dispose();
  }

  private AfterCommitActions _afterCommitActions = null!;
  private ICommittedChangeAnnouncer _announcer = null!;
  private GastronomyAppDbContext _database = null!;
  private SqliteInMemoryFixture _fixture = null!;
  private ChangeAnnouncementInterceptor _interceptor = null!;
  private SeededDomain _seeded = null!;

  [Test]
  public async Task SavingChangesAsync_ThreeNewCatalogItems_EnqueuesOneConfigurationChanged()
  {
    foreach (var index in Enumerable.Range(1, 3))
      _database.CatalogItems.Add(CatalogItemNamed($"Kuchen {index}", 10 + index));

    await _database.SaveChangesAsync();
    await RunTheCommittedActionsAsync();

    A.CallTo(() => _announcer.AnnounceAsync(HubEvent.ConfigurationChanged, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _announcer.AnnounceAsync(HubEvent.OrdersChanged, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task SavingChangesAsync_AnOrderWithItsStationOrders_EnqueuesOneOrdersChanged()
  {
    _database.Orders.Add(OrderWithStationOrders());

    await _database.SaveChangesAsync();
    await RunTheCommittedActionsAsync();

    A.CallTo(() => _announcer.AnnounceAsync(HubEvent.OrdersChanged, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _announcer.AnnounceAsync(HubEvent.ConfigurationChanged, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task SavingChangesAsync_AnOrderAndACatalogItem_EnqueuesBothEventsOnce()
  {
    _database.Orders.Add(OrderWithStationOrders());
    _database.CatalogItems.Add(CatalogItemNamed("Kuchen", 20));

    await _database.SaveChangesAsync();
    await RunTheCommittedActionsAsync();

    A.CallTo(() => _announcer.AnnounceAsync(HubEvent.OrdersChanged, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _announcer.AnnounceAsync(HubEvent.ConfigurationChanged, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task SavingChangesAsync_TwoSavesInOneRequestEachRaisingConfigurationChanged_SendsItOnce()
  {
    _database.CatalogItems.Add(CatalogItemNamed("Kuchen", 20));
    await _database.SaveChangesAsync();
    _database.CatalogItems.Add(CatalogItemNamed("Torte", 21));
    await _database.SaveChangesAsync();

    await RunTheCommittedActionsAsync();

    A.CallTo(() => _announcer.AnnounceAsync(HubEvent.ConfigurationChanged, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public void SavingChangesAsync_AnEventRaisedWhileNoRequestQueueIsCollecting_Throws()
  {
    _afterCommitActions.DiscardCollectedActions();
    _database.CatalogItems.Add(CatalogItemNamed("Kuchen", 20));

    Assert.That(async () => await _database.SaveChangesAsync(), Throws.InvalidOperationException.With.Message.Contains("ConfigurationChanged"));
  }

  [Test]
  public async Task SavingChangesAsync_AFestivalWhoseCounterMoved_EnqueuesBothEventsOnce()
  {
    var festival = await _database.Festivals.SingleAsync(candidate => candidate.Id == _seeded.FestivalId);
    festival.NextOrderNumber++;

    await _database.SaveChangesAsync();
    await RunTheCommittedActionsAsync();

    A.CallTo(() => _announcer.AnnounceAsync(HubEvent.ConfigurationChanged, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _announcer.AnnounceAsync(HubEvent.OrdersChanged, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task SavingChangesAsync_OnlyANewDeviceWhoseRaisesListIsEmpty_EnqueuesNothing()
  {
    _database.Devices.Add(new()
                          {
                            Id = Guid.NewGuid(),
                            Language = "de",
                            TokenHash = [1],
                            TokenSalt = [1],
                            TokenIterations = 1,
                            TokenAlgorithm = "test",
                            TokenLookupId = "lookup",
                            CreatedAtUtc = DateTime.UtcNow,
                            LastSeenAtUtc = DateTime.UtcNow
                          });

    await _database.SaveChangesAsync();
    await RunTheCommittedActionsAsync();

    A.CallTo(_announcer).MustNotHaveHappened();
  }

  [Test]
  public async Task SavingChangesAsync_ASaveThatIsRolledBack_SendsNothing()
  {
    _database.CatalogItems.Add(CatalogItemNamed("Kuchen", 20));

    await _database.SaveChangesAsync();
    _afterCommitActions.DiscardCollectedActions();

    A.CallTo(_announcer).MustNotHaveHappened();
  }

  [Test]
  public void SavingChangesAsync_AnEntityTypeWithoutARaisesAttribute_ThrowsNamingThatType()
  {
    using ContextWithAnEntityWithoutRaisesAttribute context = new(new DbContextOptionsBuilder<ContextWithAnEntityWithoutRaisesAttribute>().UseSqlite(_fixture.Connection).AddInterceptors(_interceptor).Options);
    context.Entities.Add(new() { Id = Guid.NewGuid() });

    Assert.That(async () => await context.SaveChangesAsync(), Throws.InvalidOperationException.With.Message.Contains(typeof(EntityWithoutRaisesAttribute).FullName));
  }

  [Test]
  public void SavingChangesAsync_EveryEntityTypeTheDatabaseStores_CarriesARaisesAttribute()
  {
    Assert.Multiple(() =>
                    {
                      foreach (var entityType in _database.Model.GetEntityTypes())
                        Assert.That(entityType.ClrType.GetCustomAttributes(typeof(RaisesAttribute), false), Has.Length.EqualTo(1), entityType.ClrType.Name);
                    });
  }

  private async Task RunTheCommittedActionsAsync()
  {
    foreach (var committedAction in _afterCommitActions.TakeCollectedActions())
      await committedAction(CancellationToken.None);
  }

  private CatalogItem CatalogItemNamed(string name, int sortOrder)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             CategoryId = _seeded.FoodCategoryId,
             SortOrder = sortOrder,
             IsActive = true
           };
  }

  private Order OrderWithStationOrders()
  {
    var orderId = Guid.NewGuid();

    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = _seeded.FestivalId,
                    GlobalOrderNumber = 1,
                    StaffMemberId = _seeded.StaffMemberId,
                    TableName = "Tisch 12",
                    CreatedAtUtc = DateTime.UtcNow
                  };

    foreach (var stationId in new[] { _seeded.KitchenStationId, _seeded.BarStationId })
      order.StationOrders.Add(new()
                              {
                                Id = Guid.NewGuid(),
                                OrderId = orderId,
                                FestivalId = _seeded.FestivalId,
                                StationId = stationId,
                                StationOrderNumber = 1,
                                DeliveryMode = DeliveryMode.Together
                              });

    return order;
  }
}
