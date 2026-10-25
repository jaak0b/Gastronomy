using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class IngredientAdministrationServiceTest
{
  [Test]
  public async Task CreateAsync_AStoredFestival_StoresTheIngredientWithItsUnitAndAnUnlimitedRowAtThatFestival()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    Unit unit = new()
                {
                  Id = Guid.NewGuid(),
                  Name = "Portion",
                  IsActive = true
                };
    fixture.DbContext.Units.Add(unit);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    IngredientAdministrationService service = new(new IngredientRepository(fixture.DbContext), new FestivalRepository(fixture.DbContext, new(), new()), new IngredientService(), new UnitRepository(fixture.DbContext));

    ErrorOr<Ingredient> created = await service.CreateAsync("Senf", unit.Id, TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    var storedIngredient = await fixture.DbContext.Ingredients.Include(ingredient => ingredient.Unit).SingleAsync(TestContext.CurrentContext.CancellationToken);
    var storedRow = await fixture.DbContext.FestivalIngredients.SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(storedIngredient.Id, Is.EqualTo(created.Value.Id));
                      Assert.That(storedIngredient.UnitId, Is.EqualTo(unit.Id));
                      Assert.That(storedIngredient.Unit.Name, Is.EqualTo("Portion"));
                      Assert.That(storedRow.FestivalId, Is.EqualTo(seeded.FestivalId));
                      Assert.That(storedRow.IngredientId, Is.EqualTo(created.Value.Id));
                      Assert.That(storedRow.AvailableAmount, Is.Null);
                    });
  }
}
