using ErrorOr;
using GastronomyApp.Contracts.Enums;
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
  public async Task CreateAsync_AStoredFestival_StoresTheIngredientWithAnUnlimitedRowAtThatFestival()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    IngredientAdministrationService service = new(new IngredientRepository(fixture.DbContext), new FestivalRepository(fixture.DbContext, new()));

    ErrorOr<Ingredient> created = await service.CreateAsync("Senf", IngredientUnit.Gram, TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    var storedIngredient = await fixture.DbContext.Ingredients.SingleAsync(TestContext.CurrentContext.CancellationToken);
    var storedRow = await fixture.DbContext.FestivalIngredients.SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(storedIngredient.Id, Is.EqualTo(created.Value.Id));
                      Assert.That(storedIngredient.Unit, Is.EqualTo(IngredientUnit.Gram));
                      Assert.That(storedRow.FestivalId, Is.EqualTo(seeded.FestivalId));
                      Assert.That(storedRow.IngredientId, Is.EqualTo(created.Value.Id));
                      Assert.That(storedRow.AvailableAmount, Is.Null);
                    });
  }
}
