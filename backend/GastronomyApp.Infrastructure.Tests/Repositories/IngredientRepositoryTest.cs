using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class IngredientRepositoryTest
{
  [Test]
  public async Task FindAllOrderedByNameAsync_TwoStoredIngredients_ReadsThemBackByNameWithTheirUnit()
  {
    using SqliteInMemoryFixture fixture = new();
    IngredientRepository repository = new(fixture.DbContext);
    await repository.AddAsync(BuildIngredient("Senf", IngredientUnit.Gram), TestContext.CurrentContext.CancellationToken);
    await repository.AddAsync(BuildIngredient("Apfelsaft", IngredientUnit.Millilitre), TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    IReadOnlyList<Ingredient> ingredients = await repository.FindAllOrderedByNameAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(ingredients.Select(ingredient => ingredient.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "Apfelsaft",
                                               "Senf"
                                             }));
                      Assert.That(ingredients.Select(ingredient => ingredient.Unit),
                                  Is.EqualTo(new[]
                                             {
                                               IngredientUnit.Millilitre,
                                               IngredientUnit.Gram
                                             }));
                    });
  }

  [Test]
  public async Task FindAllWithRecipeLinesOrderedByNameAsync_OneIngredientInARecipe_ReadsThemByNameWithEachRecipeLineAndItsArticle()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var mustard = BuildIngredient("Senf", IngredientUnit.Gram);
    fixture.DbContext.Ingredients.Add(mustard);
    fixture.DbContext.Ingredients.Add(BuildIngredient("Apfelsaft", IngredientUnit.Millilitre));
    fixture.DbContext.CatalogItemIngredients.Add(BuildRecipeLine(seeded.SausageItemId, mustard.Id));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();
    IngredientRepository repository = new(fixture.DbContext);

    IReadOnlyList<Ingredient> ingredients = await repository.FindAllWithRecipeLinesOrderedByNameAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(ingredients.Select(ingredient => ingredient.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "Apfelsaft",
                                               "Senf"
                                             }));
                      Assert.That(ingredients.Select(ingredient => ingredient.CatalogItems.Count),
                                  Is.EqualTo(new[]
                                             {
                                               0,
                                               1
                                             }));
                      Assert.That(ingredients.Single(ingredient => ingredient.Name == "Senf").CatalogItems.Single().CatalogItem.Name, Is.EqualTo("Bratwurst"));
                    });
  }

  [Test]
  public async Task FindWithRecipeLinesAsync_AnIngredientInTwoRecipes_CarriesEveryRecipeLineWithItsArticle()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var mustard = BuildIngredient("Senf", IngredientUnit.Gram);
    fixture.DbContext.Ingredients.Add(mustard);
    fixture.DbContext.CatalogItemIngredients.Add(BuildRecipeLine(seeded.SausageItemId, mustard.Id));
    fixture.DbContext.CatalogItemIngredients.Add(BuildRecipeLine(seeded.LemonadeItemId, mustard.Id));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();
    IngredientRepository repository = new(fixture.DbContext);

    var found = await repository.FindWithRecipeLinesAsync(mustard.Id, TestContext.CurrentContext.CancellationToken);

    Assert.That(found!.CatalogItems.Select(recipeLine => recipeLine.CatalogItem.Name),
                Is.EquivalentTo(new[]
                                {
                                  "Bratwurst",
                                  "Limonade"
                                }));
  }

  [Test]
  public async Task FindWithRecipeLinesAsync_AnUnknownIngredient_FindsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    IngredientRepository repository = new(fixture.DbContext);

    var found = await repository.FindWithRecipeLinesAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.Null);
  }

  [Test]
  public async Task SaveChangesAsync_ASecondIngredientWithTheSameNameInOtherCase_IsRejectedByTheUniqueIndex()
  {
    using SqliteInMemoryFixture fixture = new();
    fixture.DbContext.Ingredients.Add(BuildIngredient("Senf", IngredientUnit.Gram));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await using var second = fixture.CreateContext();
    second.Ingredients.Add(BuildIngredient("SENF", IngredientUnit.Gram));

    Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));
  }

  private CatalogItemIngredient BuildRecipeLine(Guid catalogItemId, Guid ingredientId)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             CatalogItemId = catalogItemId,
             IngredientId = ingredientId,
             Amount = 20
           };
  }

  private Ingredient BuildIngredient(string name, IngredientUnit unit)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             Unit = unit,
             IsActive = true
           };
  }
}
