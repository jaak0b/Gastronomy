using ErrorOr;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class FestivalMenuServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _clock = new(new(_now));
    await MoveTheFestivalAsync(_now.AddHours(-5), _now.AddHours(5));
    _service = new(_fixture.DbContext, new CoreMappingFactory().Create(), new(_fixture.DbContext, _clock), new(_fixture.DbContext, new(), _clock));
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

  private FakeTimeProvider _clock = null!;
  private SqliteInMemoryFixture _fixture = null!;
  private SeededDomain _seeded = null!;
  private FestivalMenuService _service = null!;

  [Test]
  public async Task PutOnTheMenuAsync_AFestivalThatDoesNotExist_IsRefusedAsNotFound()
  {
    ErrorOr<SavedItemView> putOn = await _service.PutOnTheMenuAsync(Guid.NewGuid(), _seeded.SausageItemId, RequestFor(350, _seeded.KitchenStationId), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(putOn.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(putOn.RefusalMessageKey(), Is.EqualTo("FestivalNotFound"));
                    });
  }

  [Test]
  public async Task PutOnTheMenuAsync_AnArticleThatDoesNotExist_IsRefusedAsNotFound()
  {
    ErrorOr<SavedItemView> putOn = await _service.PutOnTheMenuAsync(_seeded.FestivalId, Guid.NewGuid(), RequestFor(350, _seeded.KitchenStationId), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(putOn.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(putOn.RefusalMessageKey(), Is.EqualTo("ItemNotFound"));
                    });
  }

  [Test]
  public async Task PutOnTheMenuAsync_AStationThatIsNotAtTheFestival_NamesTheStationItRefused()
  {
    var strangerStationId = Guid.NewGuid();

    ErrorOr<SavedItemView> putOn = await _service.PutOnTheMenuAsync(_seeded.FestivalId, _seeded.SausageItemId, RequestFor(350, strangerStationId), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(putOn.IsError, Is.True);
                      Assert.That(putOn.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(putOn.RefusalDescription(), Does.Contain(strangerStationId.ToString()));
                    });
  }

  [Test]
  public async Task PutOnTheMenuAsync_NoStationAtAll_FailsBecauseNobodyWouldPrepareTheItem()
  {
    var newItemId = await AddCatalogItemAsync("Pommes");

    ErrorOr<SavedItemView> putOn = await _service.PutOnTheMenuAsync(_seeded.FestivalId, newItemId, RequestFor(350), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(putOn.IsError, Is.True);
                      Assert.That(putOn.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.itemNeedsAStation"));
                    });
  }

  [Test]
  public async Task PutOnTheMenuAsync_AnItemThatIsNotOnTheMenuYet_StoresTheRowAndItsStationAndAnswersTheItem()
  {
    var newItemId = await AddCatalogItemAsync("Pommes");

    ErrorOr<SavedItemView> putOn = await _service.PutOnTheMenuAsync(_seeded.FestivalId, newItemId, RequestFor(350, _seeded.KitchenStationId), TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();
    var menuRows = await readContext.FestivalCatalogItems.Where(row => row.FestivalId == _seeded.FestivalId && row.CatalogItemId == newItemId).ToListAsync(TestContext.CurrentContext.CancellationToken);
    var assignments = await readContext.ItemStationAssignments.Where(assignment => assignment.FestivalId == _seeded.FestivalId && assignment.CatalogItemId == newItemId).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(putOn.Value.ItemId, Is.EqualTo(newItemId));
                      Assert.That(menuRows, Has.Count.EqualTo(1));
                      Assert.That(menuRows[0].PriceCents, Is.EqualTo(350));
                      Assert.That(menuRows[0].IsAvailable, Is.True);
                      Assert.That(assignments.Select(assignment => assignment.StationId), Is.EqualTo(new[] { _seeded.KitchenStationId }));
                    });
  }

  [Test]
  public async Task PutOnTheMenuAsync_TheSameStationTwice_StoresOneAssignmentForIt()
  {
    var newItemId = await AddCatalogItemAsync("Pommes");

    await _service.PutOnTheMenuAsync(_seeded.FestivalId, newItemId, RequestFor(350, _seeded.KitchenStationId, _seeded.KitchenStationId), TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();

    Assert.That(await readContext.ItemStationAssignments.CountAsync(assignment => assignment.CatalogItemId == newItemId, TestContext.CurrentContext.CancellationToken), Is.EqualTo(1));
  }

  [Test]
  public async Task PutOnTheMenuAsync_AnItemAlreadyOnTheMenu_KeepsTheRowTakesTheNewPriceAndReplacesTheStations()
  {
    var menuRowId = (await _fixture.DbContext.FestivalCatalogItems.SingleAsync(row => row.CatalogItemId == _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken)).Id;
    _fixture.DbContext.ChangeTracker.Clear();

    await _service.PutOnTheMenuAsync(_seeded.FestivalId, _seeded.SausageItemId, RequestFor(400, _seeded.BarStationId), TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();
    var menuRows = await readContext.FestivalCatalogItems.Where(row => row.CatalogItemId == _seeded.SausageItemId).ToListAsync(TestContext.CurrentContext.CancellationToken);
    var assignments = await readContext.ItemStationAssignments.Where(assignment => assignment.CatalogItemId == _seeded.SausageItemId).ToListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(menuRows.Select(row => row.Id), Is.EqualTo(new[] { menuRowId }));
                      Assert.That(menuRows[0].PriceCents, Is.EqualTo(400));
                      Assert.That(assignments.Select(assignment => assignment.StationId), Is.EqualTo(new[] { _seeded.BarStationId }));
                    });
  }

  [Test]
  public async Task PutOnTheMenuAsync_ARefusedRequest_StoresNothing()
  {
    var newItemId = await AddCatalogItemAsync("Pommes");

    await _service.PutOnTheMenuAsync(_seeded.FestivalId, newItemId, RequestFor(350), TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();

    Assert.That(await readContext.FestivalCatalogItems.AnyAsync(row => row.CatalogItemId == newItemId, TestContext.CurrentContext.CancellationToken), Is.False);
  }

  [Test]
  public async Task TakeOffTheMenuAsync_AFestivalRunningRightNow_LeavesTheItemOnTheMenu()
  {
    ErrorOr<Success> takenOff = await _service.TakeOffTheMenuAsync(_seeded.FestivalId, _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(takenOff.IsError, Is.True);
                      Assert.That(takenOff.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.itemStaysOnTheMenuWhileTheFestivalRuns"));
                      Assert.That(await readContext.FestivalCatalogItems.AnyAsync(row => row.CatalogItemId == _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken), Is.True);
                    });
  }

  [Test]
  public async Task TakeOffTheMenuAsync_AnItemThatIsNotOnTheMenu_FailsBecauseTheRowIsNotFound()
  {
    ErrorOr<Success> takenOff = await _service.TakeOffTheMenuAsync(_seeded.FestivalId, Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(takenOff.IsError, Is.True);
                      Assert.That(takenOff.RefusalMessageKey(), Is.EqualTo("MenuRowNotFound"));
                    });
  }

  [Test]
  public async Task TakeOffTheMenuAsync_AFestivalThatIsOver_RemovesTheRowAndItsAssignments()
  {
    await MoveTheFestivalAsync(_now.AddHours(-5), _now.AddHours(-1));

    ErrorOr<Success> takenOff = await _service.TakeOffTheMenuAsync(_seeded.FestivalId, _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(takenOff.IsError, Is.False);
                      Assert.That(await readContext.FestivalCatalogItems.AnyAsync(row => row.CatalogItemId == _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken), Is.False);
                      Assert.That(await readContext.ItemStationAssignments.AnyAsync(assignment => assignment.CatalogItemId == _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken), Is.False);
                      Assert.That(await readContext.FestivalCatalogItems.AnyAsync(row => row.CatalogItemId == _seeded.LemonadeItemId, TestContext.CurrentContext.CancellationToken), Is.True);
                    });
  }

  [Test]
  public async Task SetAvailabilityAsync_TheAvailabilityTheItemAlreadyHad_ChangesNothingAndAnswersTheItem()
  {
    ErrorOr<SavedItemView> saved = await _service.SetAvailabilityAsync(_seeded.FestivalId, _seeded.SausageItemId, new() { IsAvailable = true }, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(saved.IsError, Is.False);
                      Assert.That(saved.Value.ItemId, Is.EqualTo(_seeded.SausageItemId));
                      Assert.That((await readContext.FestivalCatalogItems.SingleAsync(row => row.CatalogItemId == _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken)).IsAvailable, Is.True);
                    });
  }

  [Test]
  public async Task SetAvailabilityAsync_AnItemThatJustSoldOut_StoresItAsUnavailable()
  {
    ErrorOr<SavedItemView> saved = await _service.SetAvailabilityAsync(_seeded.FestivalId, _seeded.SausageItemId, new() { IsAvailable = false }, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(saved.Value.ItemId, Is.EqualTo(_seeded.SausageItemId));
                      Assert.That((await readContext.FestivalCatalogItems.SingleAsync(row => row.CatalogItemId == _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken)).IsAvailable, Is.False);
                    });
  }

  [Test]
  public async Task SetAvailabilityAsync_AnItemThatIsNotOnTheMenu_FailsBecauseTheRowIsNotFound()
  {
    ErrorOr<SavedItemView> saved = await _service.SetAvailabilityAsync(_seeded.FestivalId, Guid.NewGuid(), new() { IsAvailable = false }, TestContext.CurrentContext.CancellationToken);

    Assert.That(saved.RefusalMessageKey(), Is.EqualTo("MenuRowNotFound"));
  }

  private static SaveFestivalItemRequest RequestFor(int priceCents, params Guid[] stationIds)
  {
    return new()
           {
             PriceCents = priceCents,
             StationIds = stationIds
           };
  }

  private async Task<Guid> AddCatalogItemAsync(string name)
  {
    CatalogItem item = new()
                       {
                         Id = Guid.NewGuid(),
                         Name = name,
                         CategoryId = _seeded.FoodCategoryId,
                         SortOrder = 3,
                         IsActive = true
                       };
    _fixture.DbContext.CatalogItems.Add(item);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return item.Id;
  }

  private async Task MoveTheFestivalAsync(DateTime startsAtUtc, DateTime endsAtUtc)
  {
    var festival = await _fixture.DbContext.Festivals.SingleAsync(row => row.Id == _seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    festival.StartsAtUtc = startsAtUtc;
    festival.EndsAtUtc = endsAtUtc;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
