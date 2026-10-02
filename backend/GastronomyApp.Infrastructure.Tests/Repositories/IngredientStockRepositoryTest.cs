using GastronomyApp.Contracts.Enums;
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
    var mustard = await AddIngredientAsync(fixture.DbContext, "Senf", true, seeded.FestivalId, null);
    await AddRecipeLineAsync(fixture.DbContext, seeded.SausageItemId, mustard.Id, 20);
    await AddRecipeLineAsync(fixture.DbContext, seeded.LemonadeItemId, mustard.Id, 1.5);
    var acceptance = new OrderAcceptanceComposition().Create(fixture.DbContext);
    await acceptance.AcceptAsync(RequestFor(seeded.SausageItemId, seeded.SausageItemId, seeded.LemonadeItemId), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    await acceptance.AcceptAsync(RequestFor(seeded.SausageItemId), seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

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
  public async Task FindUsedOnTheMenuAsync_OneIngredientUsedOnTheMenuAndOneNot_ListsOnlyTheUsedOnesByName()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var mustard = await AddIngredientAsync(fixture.DbContext, "Senf", true, seeded.FestivalId, null);
    var bun = await AddIngredientAsync(fixture.DbContext, "Brötchen", false, seeded.FestivalId, 40);
    await AddIngredientAsync(fixture.DbContext, "Zwiebeln", true, seeded.FestivalId, null);
    await AddRecipeLineAsync(fixture.DbContext, seeded.SausageItemId, mustard.Id, 20);
    await AddRecipeLineAsync(fixture.DbContext, seeded.SausageItemId, bun.Id, 1);

    IngredientStockRepository repository = new(fixture.DbContext);

    IReadOnlyList<FestivalIngredient> stock = await repository.FindUsedOnTheMenuAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(stock.Select(row => row.Ingredient.Name), Is.EqualTo(new[] { "Brötchen", "Senf" }));
                      Assert.That(stock[0].AvailableAmount, Is.EqualTo(40));
                    });
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
    Ingredient ingredient = new()
    {
      Id = Guid.NewGuid(),
      Name = name,
      Unit = IngredientUnit.Gram,
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

  private PlaceOrderRequest RequestFor(params Guid[] catalogItemIds)
  {
    return new()
    {
      ClientOrderId = Guid.NewGuid(),
      TableName = "Tisch 3",
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
