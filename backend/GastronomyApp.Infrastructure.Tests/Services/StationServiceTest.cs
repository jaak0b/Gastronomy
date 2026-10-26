using FakeItEasy;
using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Security;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class StationServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _announcer = A.Fake<IDeviceRevocationAnnouncer>();
    _afterCommitActions = A.Fake<IAfterCommitActions>();
    A.CallTo(() => _afterCommitActions.RunWhenCommittedAsync(A<Func<CancellationToken, Task>>._, A<CancellationToken>._)).ReturnsLazily(call => ((Func<CancellationToken, Task>)call.Arguments[0]!)(call.GetArgument<CancellationToken>(1)));
    _clock = new(new(_now));
    await MoveTheFestivalAsync(_now.AddHours(-6), _now.AddHours(5));
    _service = CreateService(_fixture.DbContext);
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

  private IAfterCommitActions _afterCommitActions = null!;
  private IDeviceRevocationAnnouncer _announcer = null!;
  private FakeTimeProvider _clock = null!;
  private SqliteInMemoryFixture _fixture = null!;
  private SeededDomain _seeded = null!;
  private StationService _service = null!;

  [Test]
  public async Task ListAsync_StationsStoredOutOfOrder_ReadsThemBySortOrderWithTheirFields()
  {
    var tentId = await AddStationAsync("Zelt", 0, true, true, false);
    _fixture.DbContext.ChangeTracker.Clear();

    var listed = await _service.ListAsync(null, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.Value.Stations.Select(station => station.StationId),
                                  Is.EqualTo(new[]
                                             {
                                               tentId,
                                               _seeded.KitchenStationId,
                                               _seeded.BarStationId
                                             }));
                      Assert.That(listed.Value.Stations[0].Name, Is.EqualTo("Zelt"));
                      Assert.That(listed.Value.Stations[0].SortOrder, Is.Zero);
                      Assert.That(listed.Value.Stations[0].IsActive, Is.True);
                      Assert.That(listed.Value.Stations[0].HasDevice, Is.True);
                      Assert.That(listed.Value.Stations[1].HasDevice, Is.False);
                    });
  }

  [Test]
  public async Task ListAsync_AFestivalThatIsNotThere_FailsBecauseTheFestivalIsNotFound()
  {
    var listed = await _service.ListAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.IsError, Is.True);
                      Assert.That(listed.RefusalMessageKey(), Is.EqualTo("FestivalNotFound"));
                    });
  }

  [Test]
  public async Task ListAsync_AFestivalThatRuns_ReadsTheItemsToHandOutAtThatFestival()
  {
    await PlaceKitchenOrderAsync();

    var listed = await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.Value.Stations.Single(station => station.StationId == _seeded.KitchenStationId).HasItemsToHandOutAtTheRunningFestival, Is.True);
                      Assert.That(listed.Value.Stations.Single(station => station.StationId == _seeded.BarStationId).HasItemsToHandOutAtTheRunningFestival, Is.False);
                    });
  }

  [Test]
  public async Task ListAsync_AFestivalThatIsOver_SaysNoStationHasItemsToHandOut()
  {
    await PlaceKitchenOrderAsync();
    await MoveTheFestivalAsync(_now.AddHours(-8), _now.AddHours(-1));

    var listed = await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(listed.Value.Stations.Select(station => station.HasItemsToHandOutAtTheRunningFestival), Is.All.False);
  }

  [Test]
  public async Task ListAsync_NoFestivalWhileOneRuns_ReadsTheItemsToHandOutAtTheRunningFestival()
  {
    await PlaceKitchenOrderAsync();

    var listed = await _service.ListAsync(null, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.Value.Stations.Single(station => station.StationId == _seeded.KitchenStationId).HasItemsToHandOutAtTheRunningFestival, Is.True);
                      Assert.That(listed.Value.Stations.Single(station => station.StationId == _seeded.BarStationId).HasItemsToHandOutAtTheRunningFestival, Is.False);
                    });
  }

  [Test]
  public async Task ListAsync_NoFestivalWhileNoneRuns_SaysNoStationHasItemsToHandOut()
  {
    await PlaceKitchenOrderAsync();
    await MoveTheFestivalAsync(_now.AddHours(-8), _now.AddHours(-1));

    var listed = await _service.ListAsync(null, TestContext.CurrentContext.CancellationToken);

    Assert.That(listed.Value.Stations.Select(station => station.HasItemsToHandOutAtTheRunningFestival), Is.All.False);
  }

  [Test]
  public async Task ListAsync_NoFestival_CountsTheArticlesSwitchingTheStationOffWouldStrand()
  {
    var tentId = await AddStationAsync("Zelt", 3, true, false, false);

    var listed = await _service.ListAsync(null, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.Value.Stations.Single(station => station.StationId == _seeded.KitchenStationId).StrandedItemCountWhenSwitchedOff, Is.EqualTo(1));
                      Assert.That(listed.Value.Stations.Single(station => station.StationId == _seeded.BarStationId).StrandedItemCountWhenSwitchedOff, Is.EqualTo(1));
                      Assert.That(listed.Value.Stations.Single(station => station.StationId == tentId).StrandedItemCountWhenSwitchedOff, Is.Zero);
                      Assert.That(listed.Value.Stations.Select(station => station.StrandedItemCountWhenRemovedFromTheFestival), Is.All.Null);
                      Assert.That(listed.Value.Stations.Select(station => station.IsAtAnyFestival), Is.All.False);
                    });
  }

  [Test]
  public async Task ListAsync_AFestival_CountsTheArticlesRemovingTheStationFromItWouldStrand()
  {
    var tentId = await AddStationAsync("Zelt", 3, true, false, false);

    var listed = await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.Value.Stations.Single(station => station.StationId == _seeded.KitchenStationId).StrandedItemCountWhenRemovedFromTheFestival, Is.EqualTo(1));
                      Assert.That(listed.Value.Stations.Single(station => station.StationId == tentId).StrandedItemCountWhenRemovedFromTheFestival, Is.Zero);
                      Assert.That(listed.Value.Stations.Select(station => station.StrandedItemCountWhenSwitchedOff), Is.All.Null);
                    });
  }

  [Test]
  public async Task ListAsync_AFestival_SaysWhichStationsTakePartInIt()
  {
    var tentId = await AddStationAsync("Zelt", 3, true, false, false);

    var listed = await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.Value.Stations.Single(station => station.StationId == _seeded.KitchenStationId).IsAtAnyFestival, Is.True);
                      Assert.That(listed.Value.Stations.Single(station => station.StationId == _seeded.BarStationId).IsAtAnyFestival, Is.True);
                      Assert.That(listed.Value.Stations.Single(station => station.StationId == tentId).IsAtAnyFestival, Is.False);
                    });
  }

  [Test]
  public async Task ListAsync_NoFestivalWithSeveralStations_RunsAsManyStatementsAsWithFewer()
  {
    var withTwoStations = await CountStatementsOfListAsync();
    await AddStationAsync("Zelt", 3, true, false, false);
    await AddStationAsync("Keller", 4, true, false, false);
    await AddStationAsync("Hof", 5, true, false, false);

    var withFiveStations = await CountStatementsOfListAsync();

    Assert.That(withFiveStations, Is.EqualTo(withTwoStations));
  }

  [Test]
  public async Task CreateAsync_AValidRequest_HandsBackTheStationTheAdminListShowsAndStoresIt()
  {
    var created = await _service.CreateAsync(RequestFor("Kueche am Zelt", 2), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = await _fixture.DbContext.Stations.SingleAsync(station => station.Name == "Kueche am Zelt", TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsError, Is.False);
                      Assert.That(created.Value.StationId, Is.EqualTo(stored.Id));
                      Assert.That(created.Value.StationId, Is.Not.EqualTo(Guid.Empty));
                      Assert.That(created.Value.Name, Is.EqualTo("Kueche am Zelt"));
                      Assert.That(created.Value.SortOrder, Is.EqualTo(2));
                      Assert.That(created.Value.IsActive, Is.True);
                      Assert.That(created.Value.HasDevice, Is.False);
                      Assert.That(created.Value.IsAtAnyFestival, Is.False);
                      Assert.That(created.Value.HasItemsToHandOutAtTheRunningFestival, Is.False);
                      Assert.That(created.Value.StrandedItemCountWhenSwitchedOff, Is.Zero);
                      Assert.That(created.Value.StrandedItemCountWhenRemovedFromTheFestival, Is.Zero);
                      Assert.That(stored.SortOrder, Is.EqualTo(2));
                      Assert.That(stored.IsActive, Is.True);
                      Assert.That(stored.DeviceId, Is.Null);
                    });
  }

  [Test]
  public async Task UpdateAsync_ANameTheStationDidNotHaveBefore_HandsBackTheStationAndStoresTheChange()
  {
    var updated = await _service.UpdateAsync(_seeded.KitchenStationId, RequestFor("Kueche am Zelt", 7), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = await _fixture.DbContext.Stations.SingleAsync(station => station.Id == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.Value.StationId, Is.EqualTo(_seeded.KitchenStationId));
                      Assert.That(stored.Name, Is.EqualTo("Kueche am Zelt"));
                      Assert.That(stored.SortOrder, Is.EqualTo(7));
                    });
  }

  [Test]
  public async Task UpdateAsync_AStationThatIsNotThere_FailsBecauseTheStationIsNotFound()
  {
    var updated = await _service.UpdateAsync(Guid.NewGuid(), RequestFor("Kueche am Zelt", 1), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.IsError, Is.True);
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("StationNotFound"));
                    });
  }

  [Test]
  public async Task DeactivateAsync_AStationThatIsNotThere_FailsBecauseTheStationIsNotFound()
  {
    var switchedOff = await _service.DeactivateAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.IsError, Is.True);
                      Assert.That(switchedOff.RefusalMessageKey(), Is.EqualTo("StationNotFound"));
                    });
  }

  [Test]
  public async Task DeactivateAsync_AStationWithOpenItemsAtTheRunningFestival_LeavesItSwitchedOn()
  {
    await PlaceKitchenOrderAsync();

    var switchedOff = await _service.DeactivateAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.IsError, Is.True);
                      Assert.That(switchedOff.RefusalMessageKey(), Is.EqualTo("errors.admin.stations.hasUnfinishedItems"));
                      Assert.That(_fixture.DbContext.Stations.Single(station => station.Id == _seeded.KitchenStationId).IsActive, Is.True);
                    });
  }

  [Test]
  public async Task DeactivateAsync_AStationThatIsTheOnlyOneMakingAnArticle_FailsBecauseTheArticleWouldHaveNoStation()
  {
    var switchedOff = await _service.DeactivateAsync(_seeded.BarStationId, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.RefusalMessageKey(), Is.EqualTo("errors.admin.stations.itemsWouldHaveNoStation"));
                      Assert.That(switchedOff.RefusalMetadata("count"), Is.EqualTo("1"));
                      Assert.That(_fixture.DbContext.Stations.Single(station => station.Id == _seeded.BarStationId).IsActive, Is.True);
                    });
  }

  [Test]
  public async Task DeactivateAsync_AStationNoItemNeeds_WithdrawsItsInvitationAndRevokesItsDevice()
  {
    var tentId = await AddStationAsync("Zelt", 3, true, true, true);
    var tent = await _fixture.DbContext.Stations.SingleAsync(station => station.Id == tentId, TestContext.CurrentContext.CancellationToken);
    var deviceId = tent.DeviceId!.Value;
    var invitationId = tent.EnrolmentInvitationId!.Value;

    var switchedOff = await _service.DeactivateAsync(tentId, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = await _fixture.DbContext.Stations.SingleAsync(station => station.Id == tentId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.IsError, Is.False);
                      Assert.That(switchedOff.Value.StationId, Is.EqualTo(tentId));
                      Assert.That(stored.IsActive, Is.False);
                      Assert.That(stored.EnrolmentInvitationId, Is.Null);
                    });

    var deviceStillStored = await _fixture.DbContext.Devices.AnyAsync(device => device.Id == deviceId, TestContext.CurrentContext.CancellationToken);
    var invitation = await _fixture.DbContext.EnrolmentInvitations.SingleAsync(candidate => candidate.Id == invitationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(deviceStillStored, Is.False);
                      Assert.That(invitation.ConsumedAtUtc, Is.EqualTo(_now));
                    });

    A.CallTo(() => _announcer.AnnounceDeviceRevokedAsync(deviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task DeactivateAsync_AStationWithoutADevice_RevokesNothing()
  {
    var tentId = await AddStationAsync("Zelt", 3, true, false, false);

    var switchedOff = await _service.DeactivateAsync(tentId, TestContext.CurrentContext.CancellationToken);

    Assert.That(switchedOff.IsError, Is.False);

    A.CallTo(() => _announcer.AnnounceDeviceRevokedAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  private SaveStationRequest RequestFor(string name, int sortOrder)
  {
    return new()
           {
             Name = name,
             SortOrder = sortOrder
           };
  }

  private StationService CreateService(GastronomyAppDbContext dbContext)
  {
    FestivalRepository festivalRepository = new(dbContext, new());
    RunningFestivalLookup runningFestival = new(festivalRepository, new(), _clock);

    return new(dbContext, new CoreMappingFactory().Create(), new(dbContext, new(dbContext, new Pbkdf2SecretHasher()), _announcer, _afterCommitActions, _clock), new(dbContext, _clock), new(new StationRepository(dbContext), runningFestival));
  }

  private async Task<int> CountStatementsOfListAsync()
  {
    QueryCounter counter = new();
    await using GastronomyAppDbContext readContext = new(new DbContextOptionsBuilder<GastronomyAppDbContext>().UseSqlite(_fixture.Connection).AddInterceptors(counter).Options);

    await CreateService(readContext).ListAsync(null, TestContext.CurrentContext.CancellationToken);

    return counter.QueryCount;
  }

  private async Task MoveTheFestivalAsync(DateTime startsAtUtc, DateTime endsAtUtc)
  {
    var festival = await _fixture.DbContext.Festivals.SingleAsync(row => row.Id == _seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    festival.StartsAtUtc = startsAtUtc;
    festival.EndsAtUtc = endsAtUtc;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<Guid> AddStationAsync(string name, int sortOrder, bool isActive, bool withDevice, bool withInvitation)
  {
    Station station = new()
                      {
                        Id = Guid.NewGuid(),
                        Name = name,
                        SortOrder = sortOrder,
                        IsActive = isActive
                      };

    if (withDevice)
    {
      Device device = new()
                      {
                        Id = Guid.NewGuid(),
                        Language = "de",
                        TokenHash = [1],
                        TokenSalt = [1],
                        TokenIterations = 1,
                        TokenAlgorithm = "PBKDF2",
                        TokenLookupId = Guid.NewGuid().ToString(),
                        CreatedAtUtc = _now
                      };
      _fixture.DbContext.Devices.Add(device);
      station.DeviceId = device.Id;
    }

    if (withInvitation)
    {
      EnrolmentInvitation invitation = new()
                                       {
                                         Id = Guid.NewGuid(),
                                         QRCodeHash = [1],
                                         QRCodeSalt = [1],
                                         QRCodeIterations = 1,
                                         QRCodeAlgorithm = "PBKDF2",
                                         CreatedAtUtc = _now,
                                         ExpiresAtUtc = _now.AddHours(1)
                                       };
      _fixture.DbContext.EnrolmentInvitations.Add(invitation);
      station.EnrolmentInvitationId = invitation.Id;
    }

    _fixture.DbContext.Stations.Add(station);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return station.Id;
  }

  private async Task PlaceKitchenOrderAsync()
  {
    var tableId = await new DomainSeeder().SeedTableAsync(_fixture.DbContext, _seeded.FestivalId, "Tisch 12", TestContext.CurrentContext.CancellationToken);
    var orderId = Guid.NewGuid();
    var stationOrderId = Guid.NewGuid();

    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = _seeded.FestivalId,
                    GlobalOrderNumber = 1,
                    StaffMemberId = _seeded.StaffMemberId,
                    TableId = tableId,
                    CreatedAtUtc = _now
                  };

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = orderId,
                                  FestivalId = _seeded.FestivalId,
                                  StationId = _seeded.KitchenStationId,
                                  StationOrderNumber = 1,
                                  DeliveryMode = DeliveryMode.Together
                                };

    stationOrder.Items.Add(new()
                           {
                             Id = Guid.NewGuid(),
                             StationOrderId = stationOrderId,
                             CatalogItemId = _seeded.SausageItemId,
                             ItemName = "Bratwurst",
                             UnitPriceCents = 350
                           });
    order.StationOrders.Add(stationOrder);

    _fixture.DbContext.Orders.Add(order);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
