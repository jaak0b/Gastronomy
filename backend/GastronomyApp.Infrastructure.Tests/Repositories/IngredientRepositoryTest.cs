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
                      Assert.That(ingredients.Select(ingredient => ingredient.Name), Is.EqualTo(new[] { "Apfelsaft", "Senf" }));
                      Assert.That(ingredients.Select(ingredient => ingredient.Unit), Is.EqualTo(new[] { IngredientUnit.Millilitre, IngredientUnit.Gram }));
                    });
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
