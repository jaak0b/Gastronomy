using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class UnitRepositoryTest
{
  [Test]
  public async Task FindAllOrderedByNameAsync_TwoStoredUnits_ReadsThemBackByName()
  {
    using SqliteInMemoryFixture fixture = new();
    UnitRepository repository = new(fixture.DbContext);
    await repository.AddAsync(UnitNamed("Portion"), TestContext.CurrentContext.CancellationToken);
    await repository.AddAsync(UnitNamed("Packung"), TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    IReadOnlyList<Unit> units = await repository.FindAllOrderedByNameAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(units.Where(unit => unit.Name is "Portion" or "Packung").Select(unit => unit.Name),
                Is.EqualTo(new[]
                           {
                             "Packung",
                             "Portion"
                           }));
  }

  [Test]
  public async Task FindAllWithIngredientsOrderedByNameAsync_AUnitInUse_CarriesItsIngredients()
  {
    using SqliteInMemoryFixture fixture = new();
    Unit portion = UnitNamed("Portion");
    fixture.DbContext.Units.Add(portion);
    fixture.DbContext.Ingredients.Add(IngredientNamed("Senf", portion));
    fixture.DbContext.Ingredients.Add(IngredientNamed("Zwiebeln", portion));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();
    UnitRepository repository = new(fixture.DbContext);

    IReadOnlyList<Unit> units = await repository.FindAllWithIngredientsOrderedByNameAsync(TestContext.CurrentContext.CancellationToken);

    var found = units.Single(unit => unit.Id == portion.Id);
    Assert.That(found.Ingredients.Select(ingredient => ingredient.Name),
                Is.EquivalentTo(new[]
                                {
                                  "Senf",
                                  "Zwiebeln"
                                }));
  }

  [Test]
  public async Task FindByIdAsync_AStoredUnit_ReadsItBack()
  {
    using SqliteInMemoryFixture fixture = new();
    Unit portion = UnitNamed("Portion");
    fixture.DbContext.Units.Add(portion);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();
    UnitRepository repository = new(fixture.DbContext);

    var found = await repository.FindByIdAsync(portion.Id, TestContext.CurrentContext.CancellationToken);

    Assert.That(found!.Name, Is.EqualTo("Portion"));
  }

  [Test]
  public async Task FindByIdAsync_AnUnknownUnit_FindsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    UnitRepository repository = new(fixture.DbContext);

    var found = await repository.FindByIdAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.Null);
  }

  [Test]
  public async Task FindWithIngredientsAsync_AUnitInUse_CarriesItsIngredients()
  {
    using SqliteInMemoryFixture fixture = new();
    Unit portion = UnitNamed("Portion");
    fixture.DbContext.Units.Add(portion);
    fixture.DbContext.Ingredients.Add(IngredientNamed("Senf", portion));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();
    UnitRepository repository = new(fixture.DbContext);

    var found = await repository.FindWithIngredientsAsync(portion.Id, TestContext.CurrentContext.CancellationToken);

    Assert.That(found!.Ingredients.Select(ingredient => ingredient.Name), Is.EqualTo(new[] { "Senf" }));
  }

  [Test]
  public async Task SaveChangesAsync_ASecondUnitWithTheSameNameInOtherCase_IsRejectedByTheUniqueIndex()
  {
    using SqliteInMemoryFixture fixture = new();
    fixture.DbContext.Units.Add(UnitNamed("Portion"));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await using var second = fixture.CreateContext();
    second.Units.Add(UnitNamed("PORTION"));

    Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));
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

  private Ingredient IngredientNamed(string name, Unit unit)
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
}
