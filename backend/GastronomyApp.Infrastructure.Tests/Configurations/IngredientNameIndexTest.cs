using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Configurations;

[TestFixture]
public sealed class IngredientNameIndexTest
{
  [Test]
  public async Task SaveChangesAsync_ASecondIngredientWithTheSameNameInOtherCase_IsRejectedByTheUniqueIndex()
  {
    using SqliteInMemoryFixture fixture = new();
    Unit portion = BuildUnit();
    fixture.DbContext.Units.Add(portion);
    fixture.DbContext.Ingredients.Add(BuildIngredient("Senf", portion));
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    await using var second = fixture.CreateContext();
    second.Ingredients.Add(BuildIngredient("SENF", portion));

    Assert.ThrowsAsync<DbUpdateException>(async () => await second.SaveChangesAsync(TestContext.CurrentContext.CancellationToken));
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

  private Unit BuildUnit()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = "Portion"
           };
  }
}
