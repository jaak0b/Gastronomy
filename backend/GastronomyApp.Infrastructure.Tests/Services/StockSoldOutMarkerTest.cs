using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class StockSoldOutMarkerTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _tableId = await new DomainSeeder().SeedTableAsync(_fixture.DbContext, _seeded.FestivalId, "Tisch 3", TestContext.CurrentContext.CancellationToken);
    _marker = new(_fixture.DbContext, new(), NullLogger<StockSoldOutMarker>.Instance);
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private SqliteInMemoryFixture _fixture = null!;
  private SeededDomain _seeded = null!;
  private StockSoldOutMarker _marker = null!;
  private Guid _tableId;

  [Test]
  public async Task MarkItemsWithoutEnoughStockSoldOutAsync_LessLeftThanOnePortion_SwitchesTheArticleOffAndStoresIt()
  {
    var mustard = await AddIngredientAsync("Senf", true, 100);
    await AddRecipeLineAsync(_seeded.SausageItemId, mustard.Id, 20);
    await OrderAsync(_seeded.SausageItemId, 5);
    await OrderAsync(_seeded.LemonadeItemId, 1);
    _fixture.DbContext.ChangeTracker.Clear();

    IReadOnlyList<FestivalCatalogItem> soldOut = await _marker.MarkItemsWithoutEnoughStockSoldOutAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    using var verification = _fixture.CreateContext();

    Assert.Multiple(() =>
                    {
                      Assert.That(soldOut.Select(menuRow => menuRow.CatalogItemId), Is.EqualTo(new[] { _seeded.SausageItemId }));
                      Assert.That(IsAvailable(verification, _seeded.SausageItemId), Is.False);
                      Assert.That(IsAvailable(verification, _seeded.LemonadeItemId), Is.True);
                    });
  }

  [Test]
  public async Task MarkItemsWithoutEnoughStockSoldOutAsync_ExactlyOnePortionLeft_KeepsTheArticleOn()
  {
    var mustard = await AddIngredientAsync("Senf", true, 100);
    await AddRecipeLineAsync(_seeded.SausageItemId, mustard.Id, 20);
    await OrderAsync(_seeded.SausageItemId, 4);

    IReadOnlyList<FestivalCatalogItem> soldOut = await _marker.MarkItemsWithoutEnoughStockSoldOutAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(soldOut, Is.Empty);
  }

  [TestCase(33d, 0.33, 99)]
  [TestCase(5d, 0.1, 49)]
  public async Task MarkItemsWithoutEnoughStockSoldOutAsync_OnePortionLeftAfterFractionalPortions_KeepsTheArticleOn(double availableAmount, double portionAmount, int portionsOrdered)
  {
    var mustard = await AddIngredientAsync("Senf", true, availableAmount);
    await AddRecipeLineAsync(_seeded.SausageItemId, mustard.Id, portionAmount);
    await OrderAsync(_seeded.SausageItemId, portionsOrdered);

    IReadOnlyList<FestivalCatalogItem> soldOut = await _marker.MarkItemsWithoutEnoughStockSoldOutAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(soldOut, Is.Empty);
  }

  [Test]
  public async Task MarkItemsWithoutEnoughStockSoldOutAsync_MoreConsumedThanWasAvailable_SwitchesTheArticleOffWithoutFailing()
  {
    var mustard = await AddIngredientAsync("Senf", true, 10);
    await AddRecipeLineAsync(_seeded.SausageItemId, mustard.Id, 5);
    await OrderAsync(_seeded.SausageItemId, 6);

    IReadOnlyList<FestivalCatalogItem> soldOut = await _marker.MarkItemsWithoutEnoughStockSoldOutAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(soldOut.Select(menuRow => menuRow.CatalogItemId), Is.EqualTo(new[] { _seeded.SausageItemId }));
  }

  [Test]
  public async Task MarkItemsWithoutEnoughStockSoldOutAsync_OnlyOneOfTwoIngredientsRunsShort_SwitchesOffOnlyTheArticlesUsingIt()
  {
    var mustard = await AddIngredientAsync("Senf", true, 100);
    var bun = await AddIngredientAsync("Brötchen", true, 50);
    await AddRecipeLineAsync(_seeded.SausageItemId, mustard.Id, 20);
    await AddRecipeLineAsync(_seeded.SausageItemId, bun.Id, 1);
    await AddRecipeLineAsync(_seeded.LemonadeItemId, bun.Id, 1);
    await OrderAsync(_seeded.SausageItemId, 5);

    IReadOnlyList<FestivalCatalogItem> soldOut = await _marker.MarkItemsWithoutEnoughStockSoldOutAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(soldOut.Select(menuRow => menuRow.CatalogItemId), Is.EqualTo(new[] { _seeded.SausageItemId }));
  }

  [Test]
  public async Task MarkItemsWithoutEnoughStockSoldOutAsync_AnIngredientWithoutALimitOrSwitchedOff_NeverSwitchesAnythingOff()
  {
    var unlimited = await AddIngredientAsync("Ketchup", true, null);
    var switchedOff = await AddIngredientAsync("Senf", false, 1);
    await AddRecipeLineAsync(_seeded.SausageItemId, unlimited.Id, 20);
    await AddRecipeLineAsync(_seeded.SausageItemId, switchedOff.Id, 20);
    await OrderAsync(_seeded.SausageItemId, 10);

    IReadOnlyList<FestivalCatalogItem> soldOut = await _marker.MarkItemsWithoutEnoughStockSoldOutAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(soldOut, Is.Empty);
  }

  [Test]
  public async Task MarkItemsWithoutEnoughStockSoldOutAsync_PlentyLeftForAnArticleSwitchedOffByHand_LeavesItOff()
  {
    var mustard = await AddIngredientAsync("Senf", true, 1000);
    await AddRecipeLineAsync(_seeded.SausageItemId, mustard.Id, 20);
    var menuRow = await _fixture.DbContext.FestivalCatalogItems.SingleAsync(row => row.CatalogItemId == _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);
    menuRow.IsAvailable = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    IReadOnlyList<FestivalCatalogItem> soldOut = await _marker.MarkItemsWithoutEnoughStockSoldOutAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    using var verification = _fixture.CreateContext();

    Assert.Multiple(() =>
                    {
                      Assert.That(soldOut, Is.Empty);
                      Assert.That(IsAvailable(verification, _seeded.SausageItemId), Is.False);
                    });
  }

  [Test]
  public async Task MarkItemsWithoutEnoughStockSoldOutAsync_OrdersOfAnotherFestival_DoNotCount()
  {
    var mustard = await AddIngredientAsync("Senf", true, 100);
    await AddRecipeLineAsync(_seeded.SausageItemId, mustard.Id, 20);
    var otherFestivalId = Guid.NewGuid();
    _fixture.DbContext.Festivals.Add(new()
                                     {
                                       Id = otherFestivalId,
                                       Name = "Herbstfest",
                                       StartsAtUtc = new(2030, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                                       EndsAtUtc = new(2030, 10, 2, 12, 0, 0, DateTimeKind.Utc),
                                       NextOrderNumber = 1,
                                       IsHidden = false
                                     });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    IReadOnlyList<FestivalCatalogItem> soldOut = await _marker.MarkItemsWithoutEnoughStockSoldOutAsync(otherFestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(soldOut, Is.Empty);
  }

  private bool IsAvailable(Infrastructure.Persistence.GastronomyAppDbContext context, Guid catalogItemId)
  {
    return context.FestivalCatalogItems.Single(row => row.CatalogItemId == catalogItemId).IsAvailable;
  }

  private async Task OrderAsync(Guid catalogItemId, int portions)
  {
    var orderId = Guid.NewGuid();
    var stationId = catalogItemId == _seeded.SausageItemId ? _seeded.KitchenStationId : _seeded.BarStationId;
    var stationOrderId = Guid.NewGuid();
    var nextNumber = await _fixture.DbContext.Orders.CountAsync(TestContext.CurrentContext.CancellationToken) + 1;

    _fixture.DbContext.Orders.Add(new()
                                  {
                                    Id = orderId,
                                    ClientOrderId = Guid.NewGuid(),
                                    FestivalId = _seeded.FestivalId,
                                    GlobalOrderNumber = nextNumber,
                                    StaffMemberId = _seeded.StaffMemberId,
                                    TableId = _tableId,
                                    CreatedAtUtc = DateTime.UtcNow
                                  });
    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = orderId,
                                  FestivalId = _seeded.FestivalId,
                                  StationId = stationId,
                                  StationOrderNumber = nextNumber,
                                  DeliveryMode = Contracts.Enums.DeliveryMode.Together
                                };

    for (var portion = 0; portion < portions; portion++)
      stationOrder.Items.Add(new()
                             {
                               Id = Guid.NewGuid(),
                               StationOrderId = stationOrderId,
                               CatalogItemId = catalogItemId,
                               ItemName = "Artikel",
                               UnitPriceCents = 350
                             });

    _fixture.DbContext.StationOrders.Add(stationOrder);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task<Ingredient> AddIngredientAsync(string name, bool isActive, double? availableAmount)
  {
    var unit = await _fixture.DbContext.Units.FirstOrDefaultAsync(candidate => candidate.Name == "Portion", TestContext.CurrentContext.CancellationToken);

    if (unit is null)
    {
      unit = new()
             {
               Id = Guid.NewGuid(),
               Name = "Portion",
               IsActive = true
             };
      _fixture.DbContext.Units.Add(unit);
    }

    Ingredient ingredient = new()
                            {
                              Id = Guid.NewGuid(),
                              Name = name,
                              UnitId = unit.Id,
                              Unit = unit,
                              IsActive = isActive
                            };
    ingredient.FestivalIngredients.Add(new()
                                       {
                                         Id = Guid.NewGuid(),
                                         FestivalId = _seeded.FestivalId,
                                         IngredientId = ingredient.Id,
                                         AvailableAmount = availableAmount
                                       });
    _fixture.DbContext.Ingredients.Add(ingredient);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return ingredient;
  }

  private async Task AddRecipeLineAsync(Guid catalogItemId, Guid ingredientId, double amount)
  {
    _fixture.DbContext.CatalogItemIngredients.Add(new()
                                                  {
                                                    Id = Guid.NewGuid(),
                                                    CatalogItemId = catalogItemId,
                                                    IngredientId = ingredientId,
                                                    Amount = amount
                                                  });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
