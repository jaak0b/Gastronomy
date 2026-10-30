using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Queries;

[TestFixture]
public sealed class UnitQueriesTest
{
  [Test]
  public async Task UsedByIngredientNames_InAProjection_NamesEveryIngredientOfThatUnitActiveOrNotSortedIgnoringCase()
  {
    using SqliteInMemoryFixture fixture = new();
    var portion = UnitNamed("Portion");
    var barrel = UnitNamed("Fass");
    fixture.DbContext.Units.AddRange(portion, barrel);
    fixture.DbContext.Ingredients.AddRange(IngredientNamed("Zwiebel", portion, true), IngredientNamed("apfel", portion, false), IngredientNamed("Birne", portion, true), IngredientNamed("Senf", barrel, true));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    var projection = fixture.DbContext.Units.Where(unit => unit.Id == portion.Id).Select(unit => unit.UsedByIngredientNames().ToList());
    var names = await projection.SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(names,
                                  Is.EqualTo(new[]
                                             {
                                               "apfel",
                                               "Birne",
                                               "Zwiebel"
                                             }));
                      Assert.That(projection.ToQueryString(), Does.Contain("\"Ingredients\""));
                    });
  }

  [Test]
  public async Task UsedByIngredientNames_AUnitNoIngredientUses_IsEmpty()
  {
    using SqliteInMemoryFixture fixture = new();
    var portion = UnitNamed("Portion");
    var barrel = UnitNamed("Fass");
    fixture.DbContext.Units.AddRange(portion, barrel);
    fixture.DbContext.Ingredients.Add(IngredientNamed("Senf", barrel, true));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    var names = await fixture.DbContext.Units.Where(unit => unit.Id == portion.Id).Select(unit => unit.UsedByIngredientNames().ToList()).SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(names, Is.Empty);
  }

  private Unit UnitNamed(string name)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name
           };
  }

  private Ingredient IngredientNamed(string name, Unit unit, bool isActive)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             UnitId = unit.Id,
             Unit = unit,
             IsActive = isActive
           };
  }
}
