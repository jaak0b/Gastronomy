using ErrorOr;
using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class IngredientAdministrationServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IIngredientRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _unitRepository = A.Fake<IUnitRepository>();
    _gram = new()
            {
              Id = Guid.NewGuid(),
              Name = "g",
              IsActive = true
            };
    _millilitre = new()
                  {
                    Id = Guid.NewGuid(),
                    Name = "ml",
                    IsActive = true
                  };
    _switchedOffUnit = new()
                       {
                         Id = Guid.NewGuid(),
                         Name = "kg",
                         IsActive = false
                       };
    _mustard = new()
               {
                 Id = Guid.NewGuid(),
                 Name = "Senf",
                 UnitId = _gram.Id,
                 Unit = _gram,
                 IsActive = true
               };

    A.CallTo(() => _repository.FindAllOrderedByNameAsync(A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult<IReadOnlyList<Ingredient>>([_mustard]));
    A.CallTo(() => _repository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Ingredient?>(null));
    A.CallTo(() => _repository.FindByIdAsync(_mustard.Id, A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult<Ingredient?>(_mustard));
    A.CallTo(() => _repository.FindWithRecipeLinesAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Ingredient?>(null));
    A.CallTo(() => _repository.FindWithRecipeLinesAsync(_mustard.Id, A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult<Ingredient?>(_mustard));
    A.CallTo(() => _unitRepository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Unit?>(null));
    A.CallTo(() => _unitRepository.FindByIdAsync(_gram.Id, A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult<Unit?>(_gram));
    A.CallTo(() => _unitRepository.FindByIdAsync(_millilitre.Id, A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult<Unit?>(_millilitre));
    A.CallTo(() => _unitRepository.FindByIdAsync(_switchedOffUnit.Id, A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult<Unit?>(_switchedOffUnit));
    A.CallTo(() => _festivalRepository.FindAllAsync(A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyCollection<Festival>>([
                                                             BuildFestival(_summerFestivalId),
                                                             BuildFestival(_autumnFestivalId)
                                                           ]));

    _service = new(_repository, _festivalRepository, new IngredientService(), _unitRepository);
  }

  private readonly Guid _summerFestivalId = Guid.NewGuid();
  private readonly Guid _autumnFestivalId = Guid.NewGuid();

  private IIngredientRepository _repository = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IUnitRepository _unitRepository = null!;
  private Ingredient _mustard = null!;
  private Unit _gram = null!;
  private Unit _millilitre = null!;
  private Unit _switchedOffUnit = null!;
  private IngredientAdministrationService _service = null!;

  [Test]
  public async Task CreateAsync_ANewName_StoresTheNameAsTypedWithItsUnitAndAnUnlimitedStockRowPerFestival()
  {
    ErrorOr<Ingredient> created = await _service.CreateAsync("Bratwurst roh", _gram.Id, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.Value.Name, Is.EqualTo("Bratwurst roh"));
                      Assert.That(created.Value.UnitId, Is.EqualTo(_gram.Id));
                      Assert.That(created.Value.Unit, Is.SameAs(_gram));
                      Assert.That(created.Value.IsActive, Is.True);
                      Assert.That(created.Value.FestivalIngredients.Select(stock => stock.FestivalId),
                                  Is.EquivalentTo(new[]
                                                  {
                                                    _summerFestivalId,
                                                    _autumnFestivalId
                                                  }));
                      Assert.That(created.Value.FestivalIngredients.All(stock => stock.IngredientId == created.Value.Id && stock.AvailableAmount == null), Is.True);
                    });
    A.CallTo(() => _repository.AddAsync(created.Value, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task CreateAsync_ANameAnotherIngredientHoldsInOtherCase_IsRefusedAsNameTaken()
  {
    ErrorOr<Ingredient> created = await _service.CreateAsync("SENF", _gram.Id, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.nameTaken"));
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(created.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("ValidationFailed"));
                    });
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CreateAsync_TheNameOfADeactivatedIngredientInOtherCase_IsRefusedNamingThatIngredient()
  {
    _mustard.IsActive = false;

    ErrorOr<Ingredient> created = await _service.CreateAsync("SENF", _gram.Id, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.nameBelongsToDeactivatedIngredient"));
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(created.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("ValidationFailed"));
                      Assert.That(created.RefusalMetadata(Refusal.MetadataKeys.Name), Is.EqualTo("Senf"));
                    });
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CreateAsync_NoUnit_IsRefusedWithoutStoring()
  {
    ErrorOr<Ingredient> created = await _service.CreateAsync("Ketchup", null, CancellationToken.None);

    Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CreateAsync_AnUnknownUnit_IsRefusedWithoutStoring()
  {
    ErrorOr<Ingredient> created = await _service.CreateAsync("Ketchup", Guid.NewGuid(), CancellationToken.None);

    Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CreateAsync_ASwitchedOffUnit_IsRefusedWithoutStoring()
  {
    ErrorOr<Ingredient> created = await _service.CreateAsync("Ketchup", _switchedOffUnit.Id, CancellationToken.None);

    Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task UpdateAsync_ItsOwnNameInOtherCaseAndANewUnit_RenamesAndChangesTheUnit()
  {
    ErrorOr<Ingredient> updated = await _service.UpdateAsync(_mustard.Id, "senf", _millilitre.Id, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.Value.Name, Is.EqualTo("senf"));
                      Assert.That(updated.Value.UnitId, Is.EqualTo(_millilitre.Id));
                      Assert.That(updated.Value.Unit, Is.SameAs(_millilitre));
                    });
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task UpdateAsync_ANewUnit_LeavesTheAmountsOfItsRecipeLinesUnchanged()
  {
    _mustard.CatalogItems.Add(BuildRecipeLine("Bratwurst", true, 20));

    ErrorOr<Ingredient> updated = await _service.UpdateAsync(_mustard.Id, "Senf", _millilitre.Id, CancellationToken.None);

    Assert.That(updated.Value.CatalogItems.Single().Amount, Is.EqualTo(20));
  }

  [Test]
  public async Task UpdateAsync_ANameAnotherIngredientHolds_IsRefusedAsNameTaken()
  {
    Ingredient ketchup = new()
                         {
                           Id = Guid.NewGuid(),
                           Name = "Ketchup",
                           UnitId = _millilitre.Id,
                           Unit = _millilitre,
                           IsActive = true
                         };
    A.CallTo(() => _repository.FindByIdAsync(ketchup.Id, A<CancellationToken>._)).Returns(Task.FromResult<Ingredient?>(ketchup));

    ErrorOr<Ingredient> updated = await _service.UpdateAsync(ketchup.Id, "Senf", _millilitre.Id, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.nameTaken"));
                      Assert.That(ketchup.Name, Is.EqualTo("Ketchup"));
                    });
  }

  [Test]
  public async Task UpdateAsync_TheNameOfADeactivatedIngredient_IsRefusedNamingThatIngredient()
  {
    _mustard.IsActive = false;
    Ingredient ketchup = new()
                         {
                           Id = Guid.NewGuid(),
                           Name = "Ketchup",
                           UnitId = _millilitre.Id,
                           Unit = _millilitre,
                           IsActive = true
                         };
    A.CallTo(() => _repository.FindByIdAsync(ketchup.Id, A<CancellationToken>._)).Returns(Task.FromResult<Ingredient?>(ketchup));

    ErrorOr<Ingredient> updated = await _service.UpdateAsync(ketchup.Id, "senf", _millilitre.Id, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.nameBelongsToDeactivatedIngredient"));
                      Assert.That(updated.RefusalMetadata(Refusal.MetadataKeys.Name), Is.EqualTo("Senf"));
                      Assert.That(ketchup.Name, Is.EqualTo("Ketchup"));
                    });
  }

  [Test]
  public async Task UpdateAsync_AnUnknownIngredient_IsRefusedAsNotFound()
  {
    ErrorOr<Ingredient> updated = await _service.UpdateAsync(Guid.NewGuid(), "Zwiebeln", _gram.Id, CancellationToken.None);

    Assert.That(updated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public async Task UpdateAsync_AnUnknownUnit_IsRefusedWithoutStoring()
  {
    ErrorOr<Ingredient> updated = await _service.UpdateAsync(_mustard.Id, "Senf", Guid.NewGuid(), CancellationToken.None);

    Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task DeactivateAsync_AnActiveIngredient_SwitchesItOff()
  {
    ErrorOr<Ingredient> deactivated = await _service.DeactivateAsync(_mustard.Id, CancellationToken.None);

    Assert.That(deactivated.Value.IsActive, Is.False);
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task DeactivateAsync_AnIngredientInTheRecipeOfAnActiveArticle_IsRefusedAndStaysSwitchedOn()
  {
    _mustard.CatalogItems.Add(BuildRecipeLine("Bratwurst", true, 20));

    ErrorOr<Ingredient> deactivated = await _service.DeactivateAsync(_mustard.Id, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(deactivated.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(deactivated.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(deactivated.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("IngredientUsedByActiveItems"));
                      Assert.That(_mustard.IsActive, Is.True);
                    });
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task DeactivateAsync_AnIngredientOnlyInTheRecipeOfASwitchedOffArticle_SwitchesItOff()
  {
    _mustard.CatalogItems.Add(BuildRecipeLine("Currywurst", false, 20));

    ErrorOr<Ingredient> deactivated = await _service.DeactivateAsync(_mustard.Id, CancellationToken.None);

    Assert.That(deactivated.Value.IsActive, Is.False);
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task DeactivateAsync_AnUnknownIngredient_IsRefusedAsNotFound()
  {
    ErrorOr<Ingredient> deactivated = await _service.DeactivateAsync(Guid.NewGuid(), CancellationToken.None);

    Assert.That(deactivated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public async Task ActivateAsync_AnInactiveIngredient_SwitchesItOn()
  {
    _mustard.IsActive = false;

    ErrorOr<Ingredient> activated = await _service.ActivateAsync(_mustard.Id, CancellationToken.None);

    Assert.That(activated.Value.IsActive, Is.True);
  }

  [Test]
  public async Task ActivateAsync_AnUnknownIngredient_IsRefusedAsNotFound()
  {
    ErrorOr<Ingredient> activated = await _service.ActivateAsync(Guid.NewGuid(), CancellationToken.None);

    Assert.That(activated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  private CatalogItemIngredient BuildRecipeLine(string itemName, bool isTheItemActive, double amount)
  {
    CatalogItem item = new()
                       {
                         Id = Guid.NewGuid(),
                         Name = itemName,
                         CategoryId = Guid.NewGuid(),
                         SortOrder = 1,
                         IsActive = isTheItemActive
                       };

    return new()
           {
             Id = Guid.NewGuid(),
             CatalogItemId = item.Id,
             IngredientId = _mustard.Id,
             Amount = amount,
             CatalogItem = item,
             Ingredient = _mustard
           };
  }

  private Festival BuildFestival(Guid festivalId)
  {
    return new()
           {
             Id = festivalId,
             Name = "Fest",
             StartsAtUtc = new(2026, 9, 1, 16, 0, 0, DateTimeKind.Utc),
             EndsAtUtc = new(2026, 9, 2, 2, 0, 0, DateTimeKind.Utc),
             NextOrderNumber = 1,
             IsHidden = false
           };
  }
}
