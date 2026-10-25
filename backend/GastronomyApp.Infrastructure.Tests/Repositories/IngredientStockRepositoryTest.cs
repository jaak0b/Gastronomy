using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class IngredientStockRepositoryTest
{
  [Test]
  public async Task SumConsumedAmountsAsync_OrdersAtTwoFestivals_AddsTheRecipeAmountOncePerOrderedUnitOfThatFestivalOnly()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var tableId = await new DomainSeeder().SeedTableAsync(fixture.DbContext, seeded.FestivalId, "Tisch 3", TestContext.CurrentContext.CancellationToken);
    var mustard = await AddIngredientAsync(fixture.DbContext, "Senf", true, seeded.FestivalId, null);
    await AddRecipeLineAsync(fixture.DbContext, seeded.SausageItemId, mustard.Id, 20);
    await AddRecipeLineAsync(fixture.DbContext, seeded.LemonadeItemId, mustard.Id, 1.5);
    var acceptance = new OrderAcceptanceComposition().Create(fixture.DbContext);
    await acceptance.AcceptAsync(RequestFor(tableId, seeded.SausageItemId, seeded.SausageItemId, seeded.LemonadeItemId), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    await acceptance.AcceptAsync(RequestFor(tableId, seeded.SausageItemId), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    IngredientStockRepository repository = new(fixture.DbContext);

    IReadOnlyDictionary<Guid, double> atTheFestival = await repository.SumConsumedAmountsAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    IReadOnlyDictionary<Guid, double> elsewhere = await repository.SumConsumedAmountsAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(atTheFestival[mustard.Id], Is.EqualTo(61.5));
                      Assert.That(elsewhere, Is.Empty);
                    });
  }

  [Test]
  public async Task SumConsumedAmountsByOrderTimeAsync_TwoOrdersOneWithSeveralItems_SumsEachOrderIntoOneUtcMomentOfThatFestivalOnly()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var tableId = await new DomainSeeder().SeedTableAsync(fixture.DbContext, seeded.FestivalId, "Tisch 3", TestContext.CurrentContext.CancellationToken);
    var mustard = await AddIngredientAsync(fixture.DbContext, "Senf", true, seeded.FestivalId, null);
    await AddRecipeLineAsync(fixture.DbContext, seeded.SausageItemId, mustard.Id, 20);
    await AddRecipeLineAsync(fixture.DbContext, seeded.LemonadeItemId, mustard.Id, 1.5);
    var acceptance = new OrderAcceptanceComposition().Create(fixture.DbContext);
    await acceptance.AcceptAsync(RequestFor(tableId, seeded.SausageItemId, seeded.SausageItemId, seeded.LemonadeItemId), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    await acceptance.AcceptAsync(RequestFor(tableId, seeded.SausageItemId), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    List<DateTime> orderTimes = await fixture.DbContext.Orders.OrderBy(order => order.GlobalOrderNumber).Select(order => order.CreatedAtUtc).ToListAsync(TestContext.CurrentContext.CancellationToken);

    IngredientStockRepository repository = new(fixture.DbContext);

    IReadOnlyDictionary<Guid, IReadOnlyDictionary<DateTime, double>> atTheFestival = await repository.SumConsumedAmountsByOrderTimeAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    IReadOnlyDictionary<Guid, IReadOnlyDictionary<DateTime, double>> elsewhere = await repository.SumConsumedAmountsByOrderTimeAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);
    List<KeyValuePair<DateTime, double>> mustardMoments = atTheFestival[mustard.Id].OrderBy(moment => moment.Key).ToList();

    Assert.Multiple(() =>
                    {
                      Assert.That(mustardMoments.Select(moment => moment.Key), Is.EqualTo(orderTimes));
                      Assert.That(mustardMoments.Select(moment => moment.Value),
                                  Is.EqualTo(new[]
                                             {
                                               41.5,
                                               20
                                             }));
                      Assert.That(mustardMoments.Select(moment => moment.Key.Kind), Is.All.EqualTo(DateTimeKind.Utc));
                      Assert.That(elsewhere, Is.Empty);
                    });
  }

  [Test]
  public async Task FindActiveUsedOnTheMenuAsync_OneIngredientUsedOnTheMenuAndOneNot_ListsOnlyTheUsedOnesByName()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var mustard = await AddIngredientAsync(fixture.DbContext, "Senf", true, seeded.FestivalId, null);
    var bun = await AddIngredientAsync(fixture.DbContext, "Brötchen", true, seeded.FestivalId, 40);
    await AddIngredientAsync(fixture.DbContext, "Zwiebeln", true, seeded.FestivalId, null);
    await AddRecipeLineAsync(fixture.DbContext, seeded.SausageItemId, mustard.Id, 20);
    await AddRecipeLineAsync(fixture.DbContext, seeded.SausageItemId, bun.Id, 1);

    IngredientStockRepository repository = new(fixture.DbContext);

    IReadOnlyList<FestivalIngredient> stock = await repository.FindActiveUsedOnTheMenuAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(stock.Select(row => row.Ingredient.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "Brötchen",
                                               "Senf"
                                             }));
                      Assert.That(stock.Select(row => row.Ingredient.Unit.Name), Is.All.EqualTo("Portion"));
                      Assert.That(stock[0].AvailableAmount, Is.EqualTo(40));
                    });
  }

  [Test]
  public async Task FindActiveUsedOnTheMenuAsync_ADeactivatedIngredientInARecipeOnTheMenu_LeavesItOut()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var mustard = await AddIngredientAsync(fixture.DbContext, "Senf", true, seeded.FestivalId, null);
    var onions = await AddIngredientAsync(fixture.DbContext, "Zwiebeln", false, seeded.FestivalId, 500);
    await AddRecipeLineAsync(fixture.DbContext, seeded.SausageItemId, mustard.Id, 20);
    await AddRecipeLineAsync(fixture.DbContext, seeded.SausageItemId, onions.Id, 15);

    IngredientStockRepository repository = new(fixture.DbContext);

    IReadOnlyList<FestivalIngredient> stock = await repository.FindActiveUsedOnTheMenuAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stock.Select(row => row.Ingredient.Name), Is.EqualTo(new[] { "Senf" }));
  }

  [Test]
  public async Task FindActiveWithAvailableAmountAsync_LimitedUnlimitedAndSwitchedOffIngredients_ReturnsOnlyTheActiveLimitedOne()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var mustard = await AddIngredientAsync(fixture.DbContext, "Senf", true, seeded.FestivalId, 500);
    await AddIngredientAsync(fixture.DbContext, "Ketchup", true, seeded.FestivalId, null);
    await AddIngredientAsync(fixture.DbContext, "Brötchen", false, seeded.FestivalId, 40);

    IngredientStockRepository repository = new(fixture.DbContext);

    IReadOnlyList<FestivalIngredient> limited = await repository.FindActiveWithAvailableAmountAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(limited.Select(row => row.IngredientId), Is.EqualTo(new[] { mustard.Id }));
  }

  [Test]
  public async Task FindAvailableMenuRowsWithRecipesAsync_OneArticleSoldOut_ReturnsTheAvailableOneWithItsRecipe()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var mustard = await AddIngredientAsync(fixture.DbContext, "Senf", true, seeded.FestivalId, 500);
    await AddRecipeLineAsync(fixture.DbContext, seeded.SausageItemId, mustard.Id, 20);
    var lemonadeRow = await fixture.DbContext.FestivalCatalogItems.SingleAsync(menuRow => menuRow.CatalogItemId == seeded.LemonadeItemId, TestContext.CurrentContext.CancellationToken);
    lemonadeRow.IsAvailable = false;
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    IngredientStockRepository repository = new(fixture.DbContext);

    IReadOnlyList<FestivalCatalogItem> available = await repository.FindAvailableMenuRowsWithRecipesAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(available.Select(menuRow => menuRow.CatalogItemId), Is.EqualTo(new[] { seeded.SausageItemId }));
                      Assert.That(available[0].CatalogItem.Ingredients.Select(recipeLine => recipeLine.Amount), Is.EqualTo(new[] { 20d }));
                    });
  }

  [Test]
  public async Task SaveChangesAsync_ASecondStockRowForTheSameFestivalAndIngredient_IsRejectedByTheUniqueIndex()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var mustard = await AddIngredientAsync(fixture.DbContext, "Senf", true, seeded.FestivalId, null);
    await using var second = fixture.CreateContext();
    second.FestivalIngredients.Add(new()
                                   {
                                     Id = Guid.NewGuid(),
                                     FestivalId = seeded.FestivalId,
                                     IngredientId = mustard.Id,
                                     AvailableAmount = 3
                                   });

    Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));
  }

  private async Task<Ingredient> AddIngredientAsync(GastronomyAppDbContext dbContext, string name, bool isActive, Guid festivalId, double? availableAmount)
  {
    var unit = await dbContext.Units.FirstOrDefaultAsync(candidate => candidate.Name == "Portion", TestContext.CurrentContext.CancellationToken);

    if (unit is null)
    {
      unit = new()
             {
               Id = Guid.NewGuid(),
               Name = "Portion",
               IsActive = true
             };
      dbContext.Units.Add(unit);
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
    dbContext.Ingredients.Add(ingredient);
    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return ingredient;
  }

  private async Task AddRecipeLineAsync(GastronomyAppDbContext dbContext, Guid catalogItemId, Guid ingredientId, double amount)
  {
    dbContext.CatalogItemIngredients.Add(new()
                                         {
                                           Id = Guid.NewGuid(),
                                           CatalogItemId = catalogItemId,
                                           IngredientId = ingredientId,
                                           Amount = amount
                                         });
    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private PlaceOrderRequest RequestFor(Guid tableId, params Guid[] catalogItemIds)
  {
    return new()
           {
             ClientOrderId = Guid.NewGuid(),
             TableId = tableId,
             Items = catalogItemIds.Select(catalogItemId => new OrderItemRequest
                                                            {
                                                              CatalogItemId = catalogItemId,
                                                              Note = null,
                                                              UnitPriceCents = 350
                                                            })
                                   .ToList()
           };
  }
}
