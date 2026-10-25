using GastronomyApp.Contracts.Catalog;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class CatalogServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _clock = new(new(_now));
    await MoveTheFestivalAsync(_now.AddHours(-5), _now.AddHours(5));
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

  [Test]
  public async Task ReadRunningFestivalAsync_NoFestivalIsRunning_AnswersAnEmptyMenu()
  {
    await MoveTheFestivalAsync(_now.AddDays(10), _now.AddDays(11));

    var catalog = await CreateService(_fixture.DbContext).ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(catalog, Is.EqualTo(CatalogView.Empty));
  }

  [Test]
  public async Task ReadRunningFestivalAsync_TheSeededFestival_CarriesItsStationsCategoriesAndPricedItems()
  {
    var catalog = await CreateService(_fixture.DbContext).ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    var sausage = catalog.Items.Single(item => item.Id == _seeded.SausageItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(catalog.Festival!.FestivalId, Is.EqualTo(_seeded.FestivalId));
                      Assert.That(catalog.Festival.Name, Is.EqualTo("Sommerfest"));
                      Assert.That(catalog.Stations.Select(station => station.Id),
                                  Is.EqualTo(new[]
                                             {
                                               _seeded.KitchenStationId,
                                               _seeded.BarStationId
                                             }));
                      Assert.That(catalog.Stations[0].Name, Is.EqualTo("Kueche"));
                      Assert.That(catalog.Categories.Select(category => category.CategoryId),
                                  Is.EqualTo(new[]
                                             {
                                               _seeded.FoodCategoryId,
                                               _seeded.DrinkCategoryId
                                             }));
                      Assert.That(catalog.Items.Select(item => item.Id),
                                  Is.EqualTo(new[]
                                             {
                                               _seeded.SausageItemId,
                                               _seeded.LemonadeItemId
                                             }));
                      Assert.That(sausage.Name, Is.EqualTo("Bratwurst"));
                      Assert.That(sausage.CategoryId, Is.EqualTo(_seeded.FoodCategoryId));
                      Assert.That(sausage.PriceCents, Is.EqualTo(350));
                      Assert.That(sausage.IsAvailable, Is.True);
                      Assert.That(sausage.StationIds, Is.EqualTo(new[] { _seeded.KitchenStationId }));
                    });
  }

  [Test]
  public async Task ReadRunningFestivalAsync_ItemsAndStationsAddedOutOfOrder_PutsThemInThePlaceTheAdminGaveThem()
  {
    var stationId = await AddStationAtTheFestivalAsync("Grill", 0);
    var itemId = await AddItemAsync("Currywurst", _seeded.FoodCategoryId, 0, stationId);

    var catalog = await CreateService(_fixture.DbContext).ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(catalog.Stations[0].Id, Is.EqualTo(stationId));
                      Assert.That(catalog.Items[0].Id, Is.EqualTo(itemId));
                    });
  }

  [Test]
  public async Task ReadRunningFestivalAsync_TwoItemsOfOneCategory_NamesThatCategoryOnce()
  {
    await AddItemAsync("Currywurst", _seeded.FoodCategoryId, 3, _seeded.KitchenStationId);

    var catalog = await CreateService(_fixture.DbContext).ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(catalog.Categories.Count(category => category.CategoryId == _seeded.FoodCategoryId), Is.EqualTo(1));
  }

  [Test]
  public async Task ReadRunningFestivalAsync_AnArticleThatIsSoldOut_StaysInTheMenuMarkedUnavailable()
  {
    var menuRow = await _fixture.DbContext.FestivalCatalogItems.SingleAsync(row => row.CatalogItemId == _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);
    menuRow.IsAvailable = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var catalog = await CreateService(_fixture.DbContext).ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(catalog.Items.Single(item => item.Id == _seeded.SausageItemId).IsAvailable, Is.False);
  }

  [Test]
  public async Task ReadRunningFestivalAsync_AnArticleWithProductionMinutesBesideTheQueue_CarriesBoth()
  {
    var sausage = await _fixture.DbContext.CatalogItems.SingleAsync(item => item.Id == _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);
    sausage.ProductionMinutes = 4.5;
    sausage.IsQueueIndependent = true;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var catalog = await CreateService(_fixture.DbContext).ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    var carried = catalog.Items.Single(item => item.Id == _seeded.SausageItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(carried.ProductionMinutes, Is.EqualTo(4.5));
                      Assert.That(carried.IsQueueIndependent, Is.True);
                    });
  }

  [Test]
  public async Task ReadRunningFestivalAsync_AnItemWhoseCategoryIsSwitchedOff_LeavesTheItemAndTheCategoryOut()
  {
    var drinks = await _fixture.DbContext.CatalogCategories.SingleAsync(category => category.Id == _seeded.DrinkCategoryId, TestContext.CurrentContext.CancellationToken);
    drinks.IsActive = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var catalog = await CreateService(_fixture.DbContext).ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(catalog.Items.Select(item => item.Id), Is.EqualTo(new[] { _seeded.SausageItemId }));
                      Assert.That(catalog.Categories.Select(category => category.CategoryId), Is.EqualTo(new[] { _seeded.FoodCategoryId }));
                    });
  }

  [Test]
  public async Task ReadRunningFestivalAsync_AnItemThatIsSwitchedOff_LeavesItOut()
  {
    var lemonade = await _fixture.DbContext.CatalogItems.SingleAsync(item => item.Id == _seeded.LemonadeItemId, TestContext.CurrentContext.CancellationToken);
    lemonade.IsActive = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var catalog = await CreateService(_fixture.DbContext).ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(catalog.Items.Select(item => item.Id), Is.EqualTo(new[] { _seeded.SausageItemId }));
  }

  [Test]
  public async Task ReadRunningFestivalAsync_AnItemAssignedToAStationThatIsSwitchedOff_LeavesThatStationOutOfTheItem()
  {
    await AssignAsync(_seeded.SausageItemId, _seeded.BarStationId);
    var kitchen = await _fixture.DbContext.Stations.SingleAsync(station => station.Id == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    kitchen.IsActive = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var catalog = await CreateService(_fixture.DbContext).ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(catalog.Stations.Select(station => station.Id), Is.EqualTo(new[] { _seeded.BarStationId }));
                      Assert.That(catalog.Items.Single(item => item.Id == _seeded.SausageItemId).StationIds, Is.EqualTo(new[] { _seeded.BarStationId }));
                    });
  }

  [Test]
  public async Task ReadRunningFestivalAsync_AnItemWhoseOnlyStationIsSwitchedOff_LeavesTheItemOut()
  {
    var kitchen = await _fixture.DbContext.Stations.SingleAsync(station => station.Id == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    kitchen.IsActive = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var catalog = await CreateService(_fixture.DbContext).ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(catalog.Items.Select(item => item.Id), Is.EqualTo(new[] { _seeded.LemonadeItemId }));
  }

  [Test]
  public async Task ReadRunningFestivalAsync_TheSameItemPreparedElsewhereAtAnotherFestival_CarriesOnlyTheStationsOfTheRunningFestival()
  {
    var lastYearId = Guid.NewGuid();
    _fixture.DbContext.Festivals.Add(new()
                                     {
                                       Id = lastYearId,
                                       Name = "Sommerfest im letzten Jahr",
                                       StartsAtUtc = _now.AddYears(-1),
                                       EndsAtUtc = _now.AddYears(-1).AddDays(2),
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

    var catalog = await CreateService(_fixture.DbContext).ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(catalog.Items.Single(item => item.Id == _seeded.SausageItemId).StationIds, Is.EqualTo(new[] { _seeded.KitchenStationId }));
  }

  [Test]
  public async Task ReadRunningFestivalAsync_AnItemNoStationPrepares_LeavesItOut()
  {
    var assignment = await _fixture.DbContext.ItemStationAssignments.SingleAsync(row => row.CatalogItemId == _seeded.LemonadeItemId, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ItemStationAssignments.Remove(assignment);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var catalog = await CreateService(_fixture.DbContext).ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(catalog.Items.Select(item => item.Id), Is.EqualTo(new[] { _seeded.SausageItemId }));
  }

  [Test]
  public async Task ReadRunningFestivalAsync_AMenuWithTwoStationsAndSeveralItems_ReadsTheCatalogInSixStatements()
  {
    await AssignAsync(_seeded.SausageItemId, _seeded.BarStationId);
    await AddItemAsync("Currywurst", _seeded.FoodCategoryId, 3, _seeded.KitchenStationId);
    QueryCounter counter = new();
    await using GastronomyAppDbContext readContext = new(new DbContextOptionsBuilder<GastronomyAppDbContext>().UseSqlite(_fixture.Connection).AddInterceptors(counter).Options);

    await CreateService(readContext).ReadRunningFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(counter.QueryCount, Is.EqualTo(6));
  }

  private CatalogService CreateService(GastronomyAppDbContext database)
  {
    RunningFestivalLookup runningFestival = new(new FestivalRepository(database, new()), new(), _clock);

    return new(database, new CoreMappingFactory().Create(), new(database, _clock), runningFestival);
  }

  private async Task MoveTheFestivalAsync(DateTime startsAtUtc, DateTime endsAtUtc)
  {
    var festival = await _fixture.DbContext.Festivals.SingleAsync(row => row.Id == _seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    festival.StartsAtUtc = startsAtUtc;
    festival.EndsAtUtc = endsAtUtc;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<Guid> AddStationAtTheFestivalAsync(string name, int sortOrder)
  {
    Station station = new()
                      {
                        Id = Guid.NewGuid(),
                        Name = name,
                        SortOrder = sortOrder,
                        IsActive = true
                      };
    _fixture.DbContext.Stations.Add(station);
    _fixture.DbContext.FestivalStations.Add(new()
                                            {
                                              Id = Guid.NewGuid(),
                                              FestivalId = _seeded.FestivalId,
                                              StationId = station.Id,
                                              NextStationOrderNumber = 1
                                            });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return station.Id;
  }

  private async Task<Guid> AddItemAsync(string name, Guid categoryId, int sortOrder, Guid stationId)
  {
    CatalogItem item = new()
                       {
                         Id = Guid.NewGuid(),
                         Name = name,
                         CategoryId = categoryId,
                         SortOrder = sortOrder,
                         IsActive = true
                       };
    _fixture.DbContext.CatalogItems.Add(item);
    _fixture.DbContext.FestivalCatalogItems.Add(new()
                                                {
                                                  Id = Guid.NewGuid(),
                                                  FestivalId = _seeded.FestivalId,
                                                  CatalogItemId = item.Id,
                                                  PriceCents = 400,
                                                  IsAvailable = true
                                                });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await AssignAsync(item.Id, stationId);

    return item.Id;
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
}
