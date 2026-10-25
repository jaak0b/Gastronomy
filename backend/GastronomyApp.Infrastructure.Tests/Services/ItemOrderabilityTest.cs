using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class ItemOrderabilityTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _orderability = new(_fixture.DbContext, new FakeTimeProvider(new(_now)));
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

  private SqliteInMemoryFixture _fixture = null!;
  private ItemOrderability _orderability = null!;
  private SeededDomain _seeded = null!;

  [Test]
  public void AnyOfTheseStationsPreparesAtAsync_NullStationIds_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _orderability.AnyOfTheseStationsPreparesAtAsync(_seeded.FestivalId, null!, CancellationToken.None), Throws.ArgumentNullException);
  }

  [Test]
  public void FindItemsStrandedByRemovingEachStationAsync_NullStationIds_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _orderability.FindItemsStrandedByRemovingEachStationAsync(_seeded.FestivalId, null!, CancellationToken.None), Throws.ArgumentNullException);
  }

  [Test]
  public void FindItemsStrandedBySwitchingOffEachStationAsync_NullStationIds_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _orderability.FindItemsStrandedBySwitchingOffEachStationAsync(null!, CancellationToken.None), Throws.ArgumentNullException);
  }

  [Test]
  public async Task FindOrderableItemIdsAsync_EveryItemPreparedAtAnActiveStation_ReturnsThemAllByTheirPlaceInTheMenu()
  {
    var orderable = await _orderability.FindOrderableItemIdsAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(orderable,
                Is.EqualTo(new[]
                           {
                             _seeded.SausageItemId,
                             _seeded.LemonadeItemId
                           }));
  }

  [Test]
  public async Task FindOrderableItemIdsAsync_ItemOnTheMenuThatNoStationPrepares_LeavesItOut()
  {
    await AddMenuItemAsync("Pommes", 3, null);

    var orderable = await _orderability.FindOrderableItemIdsAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(orderable,
                Is.EqualTo(new[]
                           {
                             _seeded.SausageItemId,
                             _seeded.LemonadeItemId
                           }));
  }

  [Test]
  public async Task FindOrderableItemIdsAsync_AnItemSwitchedOff_LeavesItOut()
  {
    var lemonade = await _fixture.DbContext.CatalogItems.SingleAsync(item => item.Id == _seeded.LemonadeItemId, TestContext.CurrentContext.CancellationToken);
    lemonade.IsActive = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var orderable = await _orderability.FindOrderableItemIdsAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(orderable, Is.EqualTo(new[] { _seeded.SausageItemId }));
  }

  [Test]
  public async Task FindOrderableItemIdsAsync_AnItemWhoseOnlyStationIsSwitchedOff_LeavesItOut()
  {
    await SwitchOffAsync(_seeded.BarStationId);

    var orderable = await _orderability.FindOrderableItemIdsAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(orderable, Is.EqualTo(new[] { _seeded.SausageItemId }));
  }

  [Test]
  public async Task AnyOfTheseStationsPreparesAtAsync_AStationThatIsNotAtTheFestival_AnswersFalse()
  {
    var answer = await _orderability.AnyOfTheseStationsPreparesAtAsync(_seeded.FestivalId, [Guid.NewGuid()], TestContext.CurrentContext.CancellationToken);

    Assert.That(answer, Is.False);
  }

  [Test]
  public async Task AnyOfTheseStationsPreparesAtAsync_AnActiveStationOfTheFestival_AnswersTrue()
  {
    var answer = await _orderability.AnyOfTheseStationsPreparesAtAsync(_seeded.FestivalId, [_seeded.KitchenStationId], TestContext.CurrentContext.CancellationToken);

    Assert.That(answer, Is.True);
  }

  [Test]
  public async Task AnyOfTheseStationsPreparesAtAsync_AStationThatIsSwitchedOff_AnswersFalse()
  {
    await SwitchOffAsync(_seeded.KitchenStationId);

    var answer = await _orderability.AnyOfTheseStationsPreparesAtAsync(_seeded.FestivalId, [_seeded.KitchenStationId], TestContext.CurrentContext.CancellationToken);

    Assert.That(answer, Is.False);
  }

  [Test]
  public async Task FindItemsStrandedByRemovingStationAsync_TheOnlyStationOfAnItem_NamesThatItem()
  {
    var stranded = await _orderability.FindItemsStrandedByRemovingStationAsync(_seeded.FestivalId, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stranded, Is.EqualTo(new[] { _seeded.SausageItemId }));
  }

  [Test]
  public async Task FindItemsStrandedByRemovingStationAsync_AStationThatSharesTheItem_NamesNothing()
  {
    await AssignAsync(_seeded.SausageItemId, _seeded.BarStationId);

    var stranded = await _orderability.FindItemsStrandedByRemovingStationAsync(_seeded.FestivalId, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stranded, Is.Empty);
  }

  [Test]
  public async Task FindItemsStrandedBySwitchingOffStationAsync_AFestivalStillToCome_NamesTheItemsOfThatFestival()
  {
    var stranded = await _orderability.FindItemsStrandedBySwitchingOffStationAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stranded, Is.EqualTo(new[] { _seeded.SausageItemId }));
  }

  [Test]
  public async Task FindItemsStrandedBySwitchingOffStationAsync_NoFestivalStillToCome_NamesNothing()
  {
    await MoveTheFestivalAsync(_now.AddDays(-3), _now.AddDays(-2));

    var stranded = await _orderability.FindItemsStrandedBySwitchingOffStationAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stranded, Is.Empty);
  }

  [Test]
  public async Task FindItemsStrandedBySwitchingOffStationAsync_AHiddenFestival_NamesNothingForIt()
  {
    var festival = await _fixture.DbContext.Festivals.SingleAsync(row => row.Id == _seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    festival.IsHidden = true;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var stranded = await _orderability.FindItemsStrandedBySwitchingOffStationAsync(_seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stranded, Is.Empty);
  }

  [Test]
  public async Task FindItemsStrandedByRemovingEachStationAsync_TheKitchenAndTheBar_NamesWhatEachWouldStrandOnItsOwn()
  {
    var stranded = await _orderability.FindItemsStrandedByRemovingEachStationAsync(_seeded.FestivalId,
                                                                                   [
                                                                                     _seeded.KitchenStationId,
                                                                                     _seeded.BarStationId
                                                                                   ],
                                                                                   TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(stranded[_seeded.KitchenStationId], Is.EqualTo(new[] { _seeded.SausageItemId }));
                      Assert.That(stranded[_seeded.BarStationId], Is.EqualTo(new[] { _seeded.LemonadeItemId }));
                    });
  }

  [Test]
  public async Task FindItemsStrandedByRemovingEachStationAsync_AStationThatIsNotAnActiveStationOfTheFestival_NamesNothingForIt()
  {
    var tentId = Guid.NewGuid();

    var stranded = await _orderability.FindItemsStrandedByRemovingEachStationAsync(_seeded.FestivalId, [tentId], TestContext.CurrentContext.CancellationToken);

    Assert.That(stranded[tentId], Is.Empty);
  }

  [Test]
  public async Task FindItemsStrandedByRemovingStationAsync_AssignmentsOfAnotherFestival_AreNotCounted()
  {
    var lastYearId = Guid.NewGuid();
    _fixture.DbContext.Festivals.Add(new()
                                     {
                                       Id = lastYearId,
                                       Name = "Letztes Jahr",
                                       StartsAtUtc = _now.AddYears(-1),
                                       EndsAtUtc = _now.AddYears(-1).AddDays(1),
                                       NextOrderNumber = 1,
                                       IsHidden = false
                                     });
    _fixture.DbContext.ItemStationAssignments.Add(new()
                                                  {
                                                    Id = Guid.NewGuid(),
                                                    FestivalId = lastYearId,
                                                    CatalogItemId = _seeded.SausageItemId,
                                                    StationId = _seeded.BarStationId
                                                  });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var stranded = await _orderability.FindItemsStrandedByRemovingStationAsync(_seeded.FestivalId, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stranded, Is.EqualTo(new[] { _seeded.SausageItemId }));
  }

  private async Task SwitchOffAsync(Guid stationId)
  {
    var station = await _fixture.DbContext.Stations.SingleAsync(row => row.Id == stationId, TestContext.CurrentContext.CancellationToken);
    station.IsActive = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task MoveTheFestivalAsync(DateTime startsAtUtc, DateTime endsAtUtc)
  {
    var festival = await _fixture.DbContext.Festivals.SingleAsync(row => row.Id == _seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    festival.StartsAtUtc = startsAtUtc;
    festival.EndsAtUtc = endsAtUtc;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task AssignAsync(Guid catalogItemId, Guid stationId)
  {
    _fixture.DbContext.ItemStationAssignments.Add(new()
                                                  {
                                                    Id = Guid.NewGuid(),
                                                    FestivalId = _seeded.FestivalId,
                                                    CatalogItemId = catalogItemId,
                                                    StationId = stationId
                                                  });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task AddMenuItemAsync(string name, int sortOrder, Guid? stationId)
  {
    CatalogItem item = new()
                       {
                         Id = Guid.NewGuid(),
                         Name = name,
                         CategoryId = _seeded.FoodCategoryId,
                         SortOrder = sortOrder,
                         IsActive = true
                       };
    _fixture.DbContext.CatalogItems.Add(item);
    _fixture.DbContext.FestivalCatalogItems.Add(new()
                                                {
                                                  Id = Guid.NewGuid(),
                                                  FestivalId = _seeded.FestivalId,
                                                  CatalogItemId = item.Id,
                                                  PriceCents = 200,
                                                  IsAvailable = true
                                                });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    if (stationId is { } id)
      await AssignAsync(item.Id, id);
  }
}
