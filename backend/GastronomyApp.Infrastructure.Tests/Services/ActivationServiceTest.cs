using ErrorOr;
using GastronomyApp.Contracts.Admin.Ingredients;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class ActivationServiceTest
{
  [Test]
  public async Task ActivateAsync_ASwitchedOffRow_SwitchesItOnAndStoresIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var ingredient = await StoreIngredientAsync(fixture, false);
    ActivationService service = new(fixture.DbContext, new CoreMappingFactory().Create());

    ErrorOr<SavedIngredientView> activated = await service.ActivateAsync<Ingredient, SavedIngredientView>(ingredient.Id, Refusal.Ingredients.IngredientNotFound, TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(activated.Value, Is.EqualTo(new SavedIngredientView(ingredient.Id)));
                      Assert.That(fixture.DbContext.Ingredients.Single().IsActive, Is.True);
                    });
  }

  [Test]
  public async Task ActivateAsync_AnUnknownRow_IsRefusedWithTheNotFoundRefusalOfItsArea()
  {
    using SqliteInMemoryFixture fixture = new();
    ActivationService service = new(fixture.DbContext, new CoreMappingFactory().Create());

    ErrorOr<SavedIngredientView> activated = await service.ActivateAsync<Ingredient, SavedIngredientView>(Guid.NewGuid(), Refusal.Ingredients.IngredientNotFound, TestContext.CurrentContext.CancellationToken);

    Assert.That(activated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  private async Task<Ingredient> StoreIngredientAsync(SqliteInMemoryFixture fixture, bool isActive)
  {
    Unit unit = new()
                {
                  Id = Guid.NewGuid(),
                  Name = "Portion",
                  IsActive = true
                };
    Ingredient ingredient = new()
                            {
                              Id = Guid.NewGuid(),
                              Name = "Senf",
                              UnitId = unit.Id,
                              Unit = unit,
                              IsActive = isActive
                            };
    fixture.DbContext.Ingredients.Add(ingredient);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    return ingredient;
  }
}
