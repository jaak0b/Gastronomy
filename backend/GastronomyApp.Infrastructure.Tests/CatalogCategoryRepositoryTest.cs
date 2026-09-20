using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests;

[TestFixture]
public sealed class CatalogCategoryRepositoryTest
{
  [Test]
  public async Task FindAllOrderedAsync_CategoriesStoredOutOfOrder_ReturnsThemBySortOrder()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    CatalogCategoryRepository repository = new(fixture.DbContext);

    IReadOnlyList<CatalogCategory> found = await repository.FindAllOrderedAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(found.Select(category => category.Id),
                Is.EqualTo(new[]
                           {
                             seeded.FoodCategoryId,
                             seeded.DrinkCategoryId
                           }));
  }

  [Test]
  public async Task HoldsActiveItemsAsync_ACategoryWhoseOnlyItemIsSwitchedOff_AnswersFalse()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    CatalogCategoryRepository repository = new(fixture.DbContext);

    Assert.That(await repository.HoldsActiveItemsAsync(seeded.FoodCategoryId, TestContext.CurrentContext.CancellationToken), Is.True);

    var sausage = await fixture.DbContext.CatalogItems.FirstAsync(item => item.Id == seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);
    sausage.IsActive = false;
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(await repository.HoldsActiveItemsAsync(seeded.FoodCategoryId, TestContext.CurrentContext.CancellationToken), Is.False);
  }

  [Test]
  public async Task AddAsync_ANewCategory_StoresItWhenTheChangesAreSaved()
  {
    using SqliteInMemoryFixture fixture = new();
    await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    CatalogCategoryRepository repository = new(fixture.DbContext);
    var categoryId = Guid.NewGuid();

    await repository.AddAsync(new()
                              {
                                Id = categoryId,
                                Name = "Nachtisch",
                                ColourHex = "#2E7D32",
                                SortOrder = 3,
                                IsActive = true
                              },
                              TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();

    Assert.That(await readContext.CatalogCategories.AnyAsync(category => category.Id == categoryId, TestContext.CurrentContext.CancellationToken), Is.True);
  }

  [Test]
  public async Task FindByIdAsync_ACategoryThatIsNotThere_ReturnsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    CatalogCategoryRepository repository = new(fixture.DbContext);

    Assert.That(await repository.FindByIdAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken), Is.Null);
  }
}
