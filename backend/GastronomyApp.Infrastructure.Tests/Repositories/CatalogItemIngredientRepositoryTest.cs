using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class CatalogItemIngredientRepositoryTest
{
  [Test]
  public async Task FindAsync_AStoredRecipeLine_ReadsItsAmountBack()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var ingredientId = await AddIngredientAsync(fixture);
    CatalogItemIngredientRepository repository = new(fixture.DbContext);
    await repository.AddAsync(new()
                              {
                                Id = Guid.NewGuid(),
                                CatalogItemId = seeded.SausageItemId,
                                IngredientId = ingredientId,
                                Amount = 12.5
                              },
                              TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    var found = await repository.FindAsync(seeded.SausageItemId, ingredientId, TestContext.CurrentContext.CancellationToken);

    Assert.That(found?.Amount, Is.EqualTo(12.5));
  }

  [Test]
  public async Task Remove_AStoredRecipeLine_DeletesIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var ingredientId = await AddIngredientAsync(fixture);
    CatalogItemIngredientRepository repository = new(fixture.DbContext);
    await repository.AddAsync(BuildLine(seeded.SausageItemId, ingredientId), TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    repository.Remove((await repository.FindAsync(seeded.SausageItemId, ingredientId, TestContext.CurrentContext.CancellationToken))!);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(await fixture.DbContext.CatalogItemIngredients.CountAsync(TestContext.CurrentContext.CancellationToken), Is.Zero);
  }

  [Test]
  public async Task SaveChangesAsync_ASecondLineForTheSameArticleAndIngredient_IsRejectedByTheUniqueIndex()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var ingredientId = await AddIngredientAsync(fixture);
    fixture.DbContext.CatalogItemIngredients.Add(BuildLine(seeded.SausageItemId, ingredientId));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await using var second = fixture.CreateContext();
    second.CatalogItemIngredients.Add(BuildLine(seeded.SausageItemId, ingredientId));

    Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));
  }

  private async Task<Guid> AddIngredientAsync(SqliteInMemoryFixture fixture)
  {
    Ingredient ingredient = new()
    {
      Id = Guid.NewGuid(),
      Name = "Senf",
      Unit = IngredientUnit.Gram,
      IsActive = true
    };
    fixture.DbContext.Ingredients.Add(ingredient);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return ingredient.Id;
  }

  private CatalogItemIngredient BuildLine(Guid catalogItemId, Guid ingredientId)
  {
    return new()
    {
      Id = Guid.NewGuid(),
      CatalogItemId = catalogItemId,
      IngredientId = ingredientId,
      Amount = 20
    };
  }
}
