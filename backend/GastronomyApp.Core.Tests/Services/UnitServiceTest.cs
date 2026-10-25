using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class UnitServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _unit = new()
            {
              Id = Guid.NewGuid(),
              Name = "g",
              IsActive = true
            };
  }

  private Unit _unit = null!;

  [Test]
  public void IngredientNamesOf_IngredientsInAnyOrder_NamesThemSortedByName()
  {
    _unit.Ingredients.Add(IngredientNamed("Senf"));
    _unit.Ingredients.Add(IngredientNamed("Apfelsaft"));
    _unit.Ingredients.Add(IngredientNamed("Zwiebeln"));

    IReadOnlyList<string> names = new UnitService().IngredientNamesOf(_unit);

    Assert.That(names,
                Is.EqualTo(new[]
                           {
                             "Apfelsaft",
                             "Senf",
                             "Zwiebeln"
                           }));
  }

  [Test]
  public void IngredientNamesOf_NoIngredientUsesTheUnit_NamesNothing()
  {
    IReadOnlyList<string> names = new UnitService().IngredientNamesOf(_unit);

    Assert.That(names, Is.Empty);
  }

  [Test]
  public void IngredientNamesOf_NoUnit_Throws()
  {
    Assert.Throws<ArgumentNullException>(() => new UnitService().IngredientNamesOf(null!));
  }

  private Ingredient IngredientNamed(string name)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             UnitId = _unit.Id,
             Unit = _unit,
             IsActive = true
           };
  }
}
