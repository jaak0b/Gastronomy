using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Queries;

[TestFixture]
public sealed class IngredientQueriesTest
{
  [Test]
  public async Task UsedByItemNames_InAProjection_NamesEveryArticleWhoseRecipeUsesItSwitchedOnOrOffSortedIgnoringCase()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var portion = UnitNamed("Portion");
    var mustard = IngredientNamed("Senf", portion);
    var ketchup = IngredientNamed("Ketchup", portion);
    fixture.DbContext.Units.Add(portion);
    fixture.DbContext.Ingredients.AddRange(mustard, ketchup);
    AddArticleWithRecipeLine(fixture, "Zwiebelkuchen", seeded.FoodCategoryId, true, mustard);
    AddArticleWithRecipeLine(fixture, "apfelstrudel", seeded.FoodCategoryId, false, mustard);
    AddArticleWithRecipeLine(fixture, "Currywurst", seeded.FoodCategoryId, true, mustard);
    AddArticleWithRecipeLine(fixture, "Pommes", seeded.FoodCategoryId, true, ketchup);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    var projection = fixture.DbContext.Ingredients.Where(ingredient => ingredient.Id == mustard.Id).Select(ingredient => ingredient.UsedByItemNames().ToList());
    var names = await projection.SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(names,
                                  Is.EqualTo(new[]
                                             {
                                               "apfelstrudel",
                                               "Currywurst",
                                               "Zwiebelkuchen"
                                             }));
                      Assert.That(projection.ToQueryString(), Does.Contain("\"CatalogItemIngredients\""));
                    });
  }

  [Test]
  public async Task UsedByItemNames_AnIngredientNoRecipeUses_IsEmpty()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var portion = UnitNamed("Portion");
    var mustard = IngredientNamed("Senf", portion);
    var ketchup = IngredientNamed("Ketchup", portion);
    fixture.DbContext.Units.Add(portion);
    fixture.DbContext.Ingredients.AddRange(mustard, ketchup);
    AddArticleWithRecipeLine(fixture, "Pommes", seeded.FoodCategoryId, true, ketchup);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    var names = await fixture.DbContext.Ingredients.Where(ingredient => ingredient.Id == mustard.Id).Select(ingredient => ingredient.UsedByItemNames().ToList()).SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(names, Is.Empty);
  }

  private void AddArticleWithRecipeLine(SqliteInMemoryFixture fixture, string itemName, Guid categoryId, bool isActive, Ingredient ingredient)
  {
    CatalogItem item = new()
                       {
                         Id = Guid.NewGuid(),
                         Name = itemName,
                         CategoryId = categoryId,
                         SortOrder = 9,
                         IsActive = isActive
                       };
    fixture.DbContext.CatalogItems.Add(item);
    fixture.DbContext.CatalogItemIngredients.Add(new()
                                                 {
                                                   Id = Guid.NewGuid(),
                                                   CatalogItemId = item.Id,
                                                   IngredientId = ingredient.Id,
                                                   Amount = 1
                                                 });
  }

  private Unit UnitNamed(string name)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name
           };
  }

  private Ingredient IngredientNamed(string name, Unit unit)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             UnitId = unit.Id,
             Unit = unit
           };
  }
}
