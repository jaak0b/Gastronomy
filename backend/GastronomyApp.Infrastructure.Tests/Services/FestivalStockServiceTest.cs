using ErrorOr;
using GastronomyApp.Contracts.Admin.Ingredients;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class FestivalStockServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _clock = new(new(_now));
    await MoveTheFestivalAsync(_now.AddHours(-2), _now.AddHours(6));
    _tableId = await new DomainSeeder().SeedTableAsync(_fixture.DbContext, _seeded.FestivalId, "Tisch 3", TestContext.CurrentContext.CancellationToken);
    _mustard = await AddIngredientAsync("Senf", true, _seeded.FestivalId, null);
    await AddRecipeLineAsync(_seeded.SausageItemId, _mustard.Id, 100);
    _service = new(_fixture.DbContext, new CoreMappingFactory().Create(), new(new(), new()), new(_fixture.DbContext, new(), NullLogger<StockSoldOutMarker>.Instance), _clock);
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private readonly DateTime _now = new(2026, 9, 26, 20, 0, 0, DateTimeKind.Utc);

  private FakeTimeProvider _clock = null!;
  private SqliteInMemoryFixture _fixture = null!;
  private Ingredient _mustard = null!;
  private SeededDomain _seeded = null!;
  private FestivalStockService _service = null!;
  private Guid _tableId;
  private int _orderNumber;

  [Test]
  public async Task ListAsync_AnIngredientOnTheMenu_CarriesTheConsumedAmountAndItsUsageFromTheFestivalsOrders()
  {
    await SetStockAsync(_mustard, 900);
    await PlaceOrderAsync(_now.AddMinutes(-90), 1);
    await PlaceOrderAsync(_now.AddMinutes(-30), 2);

    ErrorOr<AdminFestivalIngredientListView> listed = await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    var row = listed.Value.Ingredients.Single();

    Assert.Multiple(() =>
                    {
                      Assert.That(row.IngredientId, Is.EqualTo(_mustard.Id));
                      Assert.That(row.Name, Is.EqualTo("Senf"));
                      Assert.That(row.AvailableAmount, Is.EqualTo(900));
                      Assert.That(row.UsedAmount, Is.EqualTo(300));
                      Assert.That(row.Usage,
                                  Is.EqualTo(new IngredientUsagePoint[]
                                             {
                                               new(_now.AddHours(-2), 0),
                                               new(_now.AddMinutes(-90), 100),
                                               new(_now.AddMinutes(-30), 300),
                                               new(_now, 300)
                                             }));
                      Assert.That(row.Usage.Select(point => point.AtUtc.Kind), Is.All.EqualTo(DateTimeKind.Utc));
                      Assert.That(row.RunsOutAtUtcSinceStart, Is.EqualTo(_now.AddHours(4)));
                      Assert.That(row.RunsOutAtUtcFromLastTwoHours, Is.EqualTo(_now.AddHours(4)));
                      Assert.That(row.StockWarning, Is.EqualTo(StockWarning.None));
                      Assert.That(row.RunsOutSoonAtUtc, Is.Null);
                    });
  }

  [Test]
  public async Task ListAsync_AnIngredientOnTheMenu_TakesTheUsedAmountFromTheLastPointOfItsUsage()
  {
    await PlaceOrderAsync(_now.AddMinutes(-90), 1);

    ErrorOr<AdminFestivalIngredientListView> listed = await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    var row = listed.Value.Ingredients.Single();

    Assert.That(row.UsedAmount, Is.EqualTo(row.Usage[^1].UsedAmount));
  }

  [Test]
  public async Task ListAsync_StockThatRunsOutWithinTwoHours_WarnsRunsOutSoonAtTheEarlierForecast()
  {
    await MoveTheFestivalAsync(_now.AddHours(-4), _now.AddHours(6));
    await SetStockAsync(_mustard, 400);
    await PlaceOrderAsync(_now.AddMinutes(-90), 1);
    await PlaceOrderAsync(_now.AddMinutes(-30), 2);

    ErrorOr<AdminFestivalIngredientListView> listed = await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    var row = listed.Value.Ingredients.Single();

    Assert.Multiple(() =>
                    {
                      Assert.That(row.RunsOutAtUtcSinceStart, Is.EqualTo(_now.AddMinutes(80)));
                      Assert.That(row.RunsOutAtUtcFromLastTwoHours, Is.EqualTo(_now.AddMinutes(40)));
                      Assert.That(row.StockWarning, Is.EqualTo(StockWarning.RunsOutSoon));
                      Assert.That(row.RunsOutSoonAtUtc, Is.EqualTo(_now.AddMinutes(40)));
                    });
  }

  [Test]
  public async Task ListAsync_UsedUpAtAFestivalThatHasEnded_WarnsUsedUpWithoutAMoment()
  {
    await MoveTheFestivalAsync(_now.AddHours(-10), _now.AddHours(-1));
    await SetStockAsync(_mustard, 300);
    await PlaceOrderAsync(_now.AddMinutes(-90), 1);
    await PlaceOrderAsync(_now.AddMinutes(-30), 2);

    ErrorOr<AdminFestivalIngredientListView> listed = await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    var row = listed.Value.Ingredients.Single();

    Assert.Multiple(() =>
                    {
                      Assert.That(row.StockWarning, Is.EqualTo(StockWarning.UsedUp));
                      Assert.That(row.RunsOutSoonAtUtc, Is.Null);
                      Assert.That(row.Usage[^1], Is.EqualTo(new IngredientUsagePoint(_now.AddHours(-1), 300)));
                    });
  }

  [Test]
  public async Task ListAsync_AnUnknownFestival_IsRefusedAsNotFound()
  {
    ErrorOr<AdminFestivalIngredientListView> listed = await _service.ListAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(listed.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public async Task ListAsync_OrdersAtTwoFestivals_CountsOnlyTheOrdersOfThatFestival()
  {
    var otherFestivalId = await new TablePlanSeeder().AddFestivalAsync(_fixture.DbContext);
    var otherTableId = await new DomainSeeder().SeedTableAsync(_fixture.DbContext, otherFestivalId, "Tisch 9", TestContext.CurrentContext.CancellationToken);
    await PlaceOrderAsync(_now.AddMinutes(-30), 2);
    await PlaceOrderAsync(_now.AddMinutes(-20), 5, otherFestivalId, otherTableId);

    ErrorOr<AdminFestivalIngredientListView> listed = await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(listed.Value.Ingredients.Single().UsedAmount, Is.EqualTo(200));
  }

  [Test]
  public async Task ListAsync_AnIngredientUsedOnTheMenuAndOneNot_ListsOnlyTheUsedOnesByNameWithTheirUnit()
  {
    var bun = await AddIngredientAsync("Brötchen", true, _seeded.FestivalId, 40);
    await AddIngredientAsync("Zwiebeln", true, _seeded.FestivalId, null);
    await AddRecipeLineAsync(_seeded.SausageItemId, bun.Id, 1);

    ErrorOr<AdminFestivalIngredientListView> listed = await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.Value.Ingredients.Select(row => row.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "Brötchen",
                                               "Senf"
                                             }));
                      Assert.That(listed.Value.Ingredients.Select(row => row.UnitName), Is.All.EqualTo("Portion"));
                      Assert.That(listed.Value.Ingredients.Select(row => row.UnitId), Is.All.EqualTo(bun.UnitId));
                      Assert.That(listed.Value.Ingredients[0].AvailableAmount, Is.EqualTo(40));
                    });
  }

  [Test]
  public async Task ListAsync_ADeactivatedIngredientInARecipeOnTheMenu_LeavesItOut()
  {
    var onions = await AddIngredientAsync("Zwiebeln", false, _seeded.FestivalId, 500);
    await AddRecipeLineAsync(_seeded.SausageItemId, onions.Id, 15);

    ErrorOr<AdminFestivalIngredientListView> listed = await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(listed.Value.Ingredients.Select(row => row.Name), Is.EqualTo(new[] { "Senf" }));
  }

  [Test]
  public async Task ListAsync_TwoOrdersOneWithSeveralItems_SumsEachOrderIntoOneMoment()
  {
    await AddRecipeLineAsync(_seeded.LemonadeItemId, _mustard.Id, 1.5);
    await PlaceOrderAsync(_now.AddMinutes(-90),
                          0,
                          itemIds:
                          [
                            _seeded.SausageItemId,
                            _seeded.SausageItemId,
                            _seeded.LemonadeItemId
                          ]);
    await PlaceOrderAsync(_now.AddMinutes(-30), 1);

    ErrorOr<AdminFestivalIngredientListView> listed = await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(listed.Value.Ingredients.Single().Usage,
                Is.EqualTo(new IngredientUsagePoint[]
                           {
                             new(_now.AddHours(-2), 0),
                             new(_now.AddMinutes(-90), 201.5),
                             new(_now.AddMinutes(-30), 301.5),
                             new(_now, 301.5)
                           }));
  }

  [Test]
  public async Task SetAvailableAmountAsync_AnAmount_StoresItAndSwitchesOffTheArticlesItCannotCover()
  {
    ErrorOr<SavedFestivalIngredientView> saved = await _service.SetAvailableAmountAsync(_seeded.FestivalId, _mustard.Id, new() { AvailableAmount = 50 }, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();
    var stock = await readContext.FestivalIngredients.SingleAsync(row => row.IngredientId == _mustard.Id, TestContext.CurrentContext.CancellationToken);
    var sausageRow = await readContext.FestivalCatalogItems.SingleAsync(row => row.CatalogItemId == _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(saved.Value.IngredientId, Is.EqualTo(_mustard.Id));
                      Assert.That(stock.AvailableAmount, Is.EqualTo(50));
                      Assert.That(sausageRow.IsAvailable, Is.False);
                    });
  }

  [Test]
  public async Task SetAvailableAmountAsync_AnAmountThatCoversOnePortion_KeepsTheArticleOn()
  {
    await _service.SetAvailableAmountAsync(_seeded.FestivalId, _mustard.Id, new() { AvailableAmount = 100 }, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();

    Assert.That((await readContext.FestivalCatalogItems.SingleAsync(row => row.CatalogItemId == _seeded.SausageItemId, TestContext.CurrentContext.CancellationToken)).IsAvailable, Is.True);
  }

  [Test]
  public async Task SetAvailableAmountAsync_NoAmount_StoresUnlimited()
  {
    await SetStockAsync(_mustard, 500);

    ErrorOr<SavedFestivalIngredientView> saved = await _service.SetAvailableAmountAsync(_seeded.FestivalId, _mustard.Id, new() { AvailableAmount = null }, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(saved.IsError, Is.False);
                      Assert.That((await readContext.FestivalIngredients.SingleAsync(row => row.IngredientId == _mustard.Id, TestContext.CurrentContext.CancellationToken)).AvailableAmount, Is.Null);
                    });
  }

  [TestCase(-1d)]
  [TestCase(double.NaN)]
  [TestCase(double.PositiveInfinity)]
  public async Task SetAvailableAmountAsync_AnAmountBelowZeroOrNotANumber_IsRefusedAsStockInvalidAndStoresNothing(double availableAmount)
  {
    ErrorOr<SavedFestivalIngredientView> saved = await _service.SetAvailableAmountAsync(_seeded.FestivalId, _mustard.Id, new() { AvailableAmount = availableAmount }, TestContext.CurrentContext.CancellationToken);

    await using var readContext = _fixture.CreateContext();

    Assert.Multiple(async () =>
                    {
                      Assert.That(saved.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.stockInvalid"));
                      Assert.That(saved.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(saved.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("ValidationFailed"));
                      Assert.That((await readContext.FestivalIngredients.SingleAsync(row => row.IngredientId == _mustard.Id, TestContext.CurrentContext.CancellationToken)).AvailableAmount, Is.Null);
                    });
  }

  [Test]
  public async Task SetAvailableAmountAsync_AnIngredientTheFestivalHasNoRowFor_IsRefusedAsNotFound()
  {
    ErrorOr<SavedFestivalIngredientView> saved = await _service.SetAvailableAmountAsync(_seeded.FestivalId, Guid.NewGuid(), new() { AvailableAmount = 5 }, TestContext.CurrentContext.CancellationToken);

    Assert.That(saved.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  private async Task SetStockAsync(Ingredient ingredient, double? availableAmount)
  {
    var stock = await _fixture.DbContext.FestivalIngredients.SingleAsync(row => row.FestivalId == _seeded.FestivalId && row.IngredientId == ingredient.Id, TestContext.CurrentContext.CancellationToken);
    stock.AvailableAmount = availableAmount;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();
  }

  private async Task PlaceOrderAsync(DateTime createdAtUtc, int sausages, Guid? festivalId = null, Guid? tableId = null, Guid[]? itemIds = null)
  {
    _orderNumber++;
    var orderFestivalId = festivalId ?? _seeded.FestivalId;
    var orderId = Guid.NewGuid();
    Order order = new()
                  {
                    Id = orderId,
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = orderFestivalId,
                    GlobalOrderNumber = _orderNumber,
                    StaffMemberId = _seeded.StaffMemberId,
                    TableId = tableId ?? _tableId,
                    CreatedAtUtc = createdAtUtc
                  };
    StationOrder stationOrder = new()
                                {
                                  Id = Guid.NewGuid(),
                                  OrderId = orderId,
                                  FestivalId = orderFestivalId,
                                  StationId = _seeded.KitchenStationId,
                                  StationOrderNumber = _orderNumber,
                                  DeliveryMode = DeliveryMode.Together
                                };

    foreach (var catalogItemId in itemIds ?? Enumerable.Repeat(_seeded.SausageItemId, sausages))
      stationOrder.Items.Add(new()
                             {
                               Id = Guid.NewGuid(),
                               StationOrderId = stationOrder.Id,
                               CatalogItemId = catalogItemId,
                               ItemName = "Artikel",
                               UnitPriceCents = 350
                             });

    order.StationOrders.Add(stationOrder);
    _fixture.DbContext.Orders.Add(order);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();
  }

  private async Task<Ingredient> AddIngredientAsync(string name, bool isActive, Guid festivalId, double? availableAmount)
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
                                         FestivalId = festivalId,
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

  private async Task MoveTheFestivalAsync(DateTime startsAtUtc, DateTime endsAtUtc)
  {
    var festival = await _fixture.DbContext.Festivals.SingleAsync(row => row.Id == _seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    festival.StartsAtUtc = startsAtUtc;
    festival.EndsAtUtc = endsAtUtc;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();
  }
}
