using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class IngredientRepositoryTest
{
  [Test]
  public async Task FindAllOrderedByNameAsync_TwoStoredIngredients_ReadsThemBackByNameWithTheirUnitId()
  {
    using SqliteInMemoryFixture fixture = new();
    IngredientRepository repository = new(fixture.DbContext);
    Unit gram = UnitNamed("Portion");
    Unit millilitre = UnitNamed("Packung");
    fixture.DbContext.Units.AddRange(gram, millilitre);
    fixture.DbContext.Ingredients.AddRange(BuildIngredient("Senf", gram), BuildIngredient("Apfelsaft", millilitre));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
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
                      Assert.That(ingredients.Select(ingredient => ingredient.UnitId),
                                  Is.EqualTo(new[]
                                             {
                                               millilitre.Id,
                                               gram.Id
                                             }));
                    });
  }

  private Ingredient BuildIngredient(string name, Unit unit)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             UnitId = unit.Id,
             Unit = unit,
             IsActive = true
           };
  }

  private Unit UnitNamed(string name)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             IsActive = true
           };
  }
}
