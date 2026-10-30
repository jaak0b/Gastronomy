using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Configurations;

[TestFixture]
public sealed class CatalogItemIngredientIndexTest
{
  [Test]
  public async Task SaveChangesAsync_ASecondLineForTheSameArticleAndIngredient_IsRejectedByTheUniqueIndex()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    Unit unit = new()
                {
                  Id = Guid.NewGuid(),
                  Name = "Portion"
                };
    Ingredient ingredient = new()
                            {
                              Id = Guid.NewGuid(),
                              Name = "Senf",
                              UnitId = unit.Id,
                              Unit = unit
                            };
    fixture.DbContext.Units.Add(unit);
    fixture.DbContext.Ingredients.Add(ingredient);
    fixture.DbContext.CatalogItemIngredients.Add(BuildLine(seeded.SausageItemId, ingredient.Id));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await using var second = fixture.CreateContext();
    second.CatalogItemIngredients.Add(BuildLine(seeded.SausageItemId, ingredient.Id));

    Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));
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
