using ErrorOr;
using GastronomyApp.Contracts.Admin.Ingredients;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class IngredientServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _gram = AddUnit("Portion");
    _millilitre = AddUnit("Fass");
    _mustard = AddIngredient("Senf", _gram, true);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _service = new(_fixture.DbContext, new CoreMappingFactory().Create());
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private SqliteInMemoryFixture _fixture = null!;
  private SeededDomain _seeded = null!;
  private Unit _gram = null!;
  private Unit _millilitre = null!;
  private Ingredient _mustard = null!;
  private IngredientService _service = null!;

  [Test]
  public async Task ListAsync_IngredientsWithRecipes_ReadsThemByNameWithTheirUnitAndTheNamesOfTheirActiveArticlesOnly()
  {
    var apple = AddIngredient("Apfelsaft", _millilitre, false);
    await AddRecipeLineAsync(_mustard, "Currywurst", true);
    await AddRecipeLineAsync(_mustard, "Pommes", false);
    await AddRecipeLineAsync(_mustard, "Bratwurst", true);
    _fixture.DbContext.ChangeTracker.Clear();

    var list = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(list.Ingredients.Select(ingredient => ingredient.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "Apfelsaft",
                                               "Senf"
                                             }));
                      Assert.That(list.Ingredients[0].IngredientId, Is.EqualTo(apple.Id));
                      Assert.That(list.Ingredients[0].UnitName, Is.EqualTo("Fass"));
                      Assert.That(list.Ingredients[0].IsActive, Is.False);
                      Assert.That(list.Ingredients[0].ActiveItemNames, Is.Empty);
                      Assert.That(list.Ingredients[1].UnitId, Is.EqualTo(_gram.Id));
                      Assert.That(list.Ingredients[1].UnitName, Is.EqualTo("Portion"));
                      Assert.That(list.Ingredients[1].ActiveItemNames,
                                  Is.EqualTo(new[]
                                             {
                                               "Bratwurst",
                                               "Currywurst"
                                             }));
                    });
  }

  [Test]
  public async Task ListAsync_ArticleNamesInMixedCase_AreListedWithCapitalsIgnoredAndUmlautsAfterZ()
  {
    foreach (var itemName in new[]
                             {
                               "Zwiebelkuchen",
                               "Äpfelchen",
                               "apfelstrudel",
                               "Birnenkuchen"
                             })
      await AddRecipeLineAsync(_mustard, itemName, true);

    _fixture.DbContext.ChangeTracker.Clear();

    var list = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(list.Ingredients.Single().ActiveItemNames,
                Is.EqualTo(new[]
                           {
                             "apfelstrudel",
                             "Birnenkuchen",
                             "Zwiebelkuchen",
                             "Äpfelchen"
                           }));
  }

  [Test]
  public async Task CreateAsync_ANewName_StoresTheNameAsTypedWithItsUnitAndAnUnlimitedStockRowPerFestival()
  {
    ErrorOr<AdminIngredientView> created = await _service.CreateAsync(RequestFor("Bratwurst roh", _gram.Id), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = await _fixture.DbContext.Ingredients.Include(ingredient => ingredient.FestivalIngredients).SingleAsync(ingredient => ingredient.Name == "Bratwurst roh", TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.Value.IngredientId, Is.EqualTo(stored.Id));
                      Assert.That(created.Value.Name, Is.EqualTo("Bratwurst roh"));
                      Assert.That(created.Value.UnitName, Is.EqualTo("Portion"));
                      Assert.That(created.Value.IsActive, Is.True);
                      Assert.That(created.Value.ActiveItemNames, Is.Empty);
                      Assert.That(stored.UnitId, Is.EqualTo(_gram.Id));
                      Assert.That(stored.IsActive, Is.True);
                      Assert.That(stored.FestivalIngredients.Select(stock => stock.FestivalId), Is.EqualTo(new[] { _seeded.FestivalId }));
                      Assert.That(stored.FestivalIngredients.Single().AvailableAmount, Is.Null);
                    });
  }

  [Test]
  public async Task CreateAsync_ANameAnotherIngredientHoldsInOtherCase_IsRefusedAsNameTakenAndStoresNothing()
  {
    ErrorOr<AdminIngredientView> created = await _service.CreateAsync(RequestFor("SENF", _gram.Id), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.nameTaken"));
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(created.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("ValidationFailed"));
                      Assert.That(StoredIngredientCount(), Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task CreateAsync_TheNameOfADeactivatedIngredientInOtherCase_IsRefusedNamingThatIngredient()
  {
    _mustard.IsActive = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<AdminIngredientView> created = await _service.CreateAsync(RequestFor("SENF", _gram.Id), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.nameBelongsToDeactivatedIngredient"));
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(created.RefusalMetadata(Refusal.MetadataKeys.Name), Is.EqualTo("Senf"));
                      Assert.That(StoredIngredientCount(), Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task CreateAsync_NoUnit_IsRefusedWithoutStoring()
  {
    ErrorOr<AdminIngredientView> created = await _service.CreateAsync(RequestFor("Ketchup", null), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("common.errors.actionFailed"));
                      Assert.That(StoredIngredientCount(), Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task CreateAsync_AnUnknownUnit_IsRefusedWithoutStoring()
  {
    ErrorOr<AdminIngredientView> created = await _service.CreateAsync(RequestFor("Ketchup", Guid.NewGuid()), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("common.errors.actionFailed"));
                      Assert.That(StoredIngredientCount(), Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task UpdateAsync_ItsOwnNameInOtherCaseAndANewUnit_RenamesAndChangesTheUnit()
  {
    ErrorOr<SavedIngredientView> updated = await _service.UpdateAsync(_mustard.Id, RequestFor("senf", _millilitre.Id), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = await _fixture.DbContext.Ingredients.SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.Value, Is.EqualTo(new SavedIngredientView(_mustard.Id)));
                      Assert.That(stored.Name, Is.EqualTo("senf"));
                      Assert.That(stored.UnitId, Is.EqualTo(_millilitre.Id));
                    });
  }

  [Test]
  public async Task UpdateAsync_ANewUnit_LeavesTheAmountsOfItsRecipeLinesUnchanged()
  {
    await AddRecipeLineAsync(_mustard, "Bratwurst", true);

    await _service.UpdateAsync(_mustard.Id, RequestFor("Senf", _millilitre.Id), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.That(await _fixture.DbContext.CatalogItemIngredients.Select(recipeLine => recipeLine.Amount).SingleAsync(TestContext.CurrentContext.CancellationToken), Is.EqualTo(20));
  }

  [Test]
  public async Task UpdateAsync_ANameAnotherIngredientHolds_IsRefusedAsNameTakenAndKeepsTheName()
  {
    var ketchup = AddIngredient("Ketchup", _millilitre, true);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<SavedIngredientView> updated = await _service.UpdateAsync(ketchup.Id, RequestFor("Senf", _millilitre.Id), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.nameTaken"));
                      Assert.That(_fixture.DbContext.Ingredients.Single(ingredient => ingredient.Id == ketchup.Id).Name, Is.EqualTo("Ketchup"));
                    });
  }

  [Test]
  public async Task UpdateAsync_TheNameOfADeactivatedIngredient_IsRefusedNamingThatIngredientAndKeepsTheName()
  {
    _mustard.IsActive = false;
    var ketchup = AddIngredient("Ketchup", _millilitre, true);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<SavedIngredientView> updated = await _service.UpdateAsync(ketchup.Id, RequestFor("senf", _millilitre.Id), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.nameBelongsToDeactivatedIngredient"));
                      Assert.That(updated.RefusalMetadata(Refusal.MetadataKeys.Name), Is.EqualTo("Senf"));
                      Assert.That(_fixture.DbContext.Ingredients.Single(ingredient => ingredient.Id == ketchup.Id).Name, Is.EqualTo("Ketchup"));
                    });
  }

  [Test]
  public async Task UpdateAsync_AnUnknownIngredient_IsRefusedAsNotFound()
  {
    ErrorOr<SavedIngredientView> updated = await _service.UpdateAsync(Guid.NewGuid(), RequestFor("Zwiebeln", _gram.Id), TestContext.CurrentContext.CancellationToken);

    Assert.That(updated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public async Task UpdateAsync_AnUnknownUnit_IsRefusedAndKeepsTheName()
  {
    ErrorOr<SavedIngredientView> updated = await _service.UpdateAsync(_mustard.Id, RequestFor("Zwiebeln", Guid.NewGuid()), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("common.errors.actionFailed"));
                      Assert.That(_fixture.DbContext.Ingredients.Single().Name, Is.EqualTo("Senf"));
                    });
  }

  [Test]
  public async Task DeactivateAsync_AnActiveIngredient_SwitchesItOff()
  {
    ErrorOr<SavedIngredientView> deactivated = await _service.DeactivateAsync(_mustard.Id, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(deactivated.Value, Is.EqualTo(new SavedIngredientView(_mustard.Id)));
                      Assert.That(_fixture.DbContext.Ingredients.Single().IsActive, Is.False);
                    });
  }

  [Test]
  public async Task DeactivateAsync_AnIngredientInTheRecipeOfAnActiveArticle_IsRefusedAndStaysSwitchedOn()
  {
    await AddRecipeLineAsync(_mustard, "Currywurst", true);

    ErrorOr<SavedIngredientView> deactivated = await _service.DeactivateAsync(_mustard.Id, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(deactivated.RefusalMessageKey(), Is.EqualTo("common.errors.actionFailed"));
                      Assert.That(deactivated.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(deactivated.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("IngredientUsedByActiveItems"));
                      Assert.That(_fixture.DbContext.Ingredients.Single().IsActive, Is.True);
                    });
  }

  [Test]
  public async Task DeactivateAsync_AnIngredientOnlyInTheRecipeOfASwitchedOffArticle_SwitchesItOff()
  {
    await AddRecipeLineAsync(_mustard, "Currywurst", false);

    ErrorOr<SavedIngredientView> deactivated = await _service.DeactivateAsync(_mustard.Id, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(deactivated.IsError, Is.False);
                      Assert.That(_fixture.DbContext.Ingredients.Single().IsActive, Is.False);
                    });
  }

  [Test]
  public async Task DeactivateAsync_AnUnknownIngredient_IsRefusedAsNotFound()
  {
    ErrorOr<SavedIngredientView> deactivated = await _service.DeactivateAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(deactivated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  private SaveIngredientRequest RequestFor(string name, Guid? unitId)
  {
    return new()
           {
             Name = name,
             UnitId = unitId
           };
  }

  private int StoredIngredientCount()
  {
    using var readContext = _fixture.CreateContext();

    return readContext.Ingredients.Count();
  }

  private Unit AddUnit(string name)
  {
    Unit unit = new()
                {
                  Id = Guid.NewGuid(),
                  Name = name
                };
    _fixture.DbContext.Units.Add(unit);

    return unit;
  }

  private Ingredient AddIngredient(string name, Unit unit, bool isActive)
  {
    Ingredient ingredient = new()
                            {
                              Id = Guid.NewGuid(),
                              Name = name,
                              UnitId = unit.Id,
                              Unit = unit,
                              IsActive = isActive
                            };
    _fixture.DbContext.Ingredients.Add(ingredient);

    return ingredient;
  }

  private async Task AddRecipeLineAsync(Ingredient ingredient, string itemName, bool isTheItemActive)
  {
    CatalogItem item = new()
                       {
                         Id = Guid.NewGuid(),
                         Name = itemName,
                         CategoryId = _seeded.FoodCategoryId,
                         SortOrder = 9,
                         IsActive = isTheItemActive
                       };
    _fixture.DbContext.CatalogItems.Add(item);
    _fixture.DbContext.CatalogItemIngredients.Add(new()
                                                  {
                                                    Id = Guid.NewGuid(),
                                                    CatalogItemId = item.Id,
                                                    IngredientId = ingredient.Id,
                                                    Amount = 20
                                                  });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
