using ErrorOr;
using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class UnitAdministrationServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IUnitRepository>();
    _gram = new()
            {
              Id = Guid.NewGuid(),
              Name = "g",
              IsActive = true
            };
    _litre = new()
             {
               Id = Guid.NewGuid(),
               Name = "l",
               IsActive = true
             };
    _millilitre = new()
                  {
                    Id = Guid.NewGuid(),
                    Name = "ml",
                    IsActive = false
                  };

    A.CallTo(() => _repository.FindAllWithIngredientsOrderedByNameAsync(A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyList<Unit>>([
                                                   _gram,
                                                   _litre
                                                 ]));
    A.CallTo(() => _repository.FindAllOrderedByNameAsync(A<CancellationToken>._))
   .ReturnsLazily(() => Task.FromResult<IReadOnlyList<Unit>>([
                                                               _gram,
                                                               _litre,
                                                               _millilitre
                                                             ]));
    A.CallTo(() => _repository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Unit?>(null));
    A.CallTo(() => _repository.FindByIdAsync(_gram.Id, A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult<Unit?>(_gram));
    A.CallTo(() => _repository.FindByIdAsync(_millilitre.Id, A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult<Unit?>(_millilitre));
    A.CallTo(() => _repository.FindWithIngredientsAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Unit?>(null));
    A.CallTo(() => _repository.FindWithIngredientsAsync(_gram.Id, A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult<Unit?>(_gram));

    _service = new(_repository, new UnitService());
  }

  private IUnitRepository _repository = null!;
  private Unit _gram = null!;
  private Unit _litre = null!;
  private Unit _millilitre = null!;
  private UnitAdministrationService _service = null!;

  [Test]
  public async Task ListAsync_TwoUnits_ReturnsTheRepositoryListWithTheirIngredients()
  {
    IReadOnlyList<Unit> units = await _service.ListAsync(CancellationToken.None);

    Assert.That(units,
                Is.EqualTo(new[]
                           {
                             _gram,
                             _litre
                           }));
    A.CallTo(() => _repository.FindAllWithIngredientsOrderedByNameAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task CreateAsync_ANewName_StoresTheNameAsTypedAndSwitchedOn()
  {
    ErrorOr<Unit> created = await _service.CreateAsync("Portion", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.Value.Name, Is.EqualTo("Portion"));
                      Assert.That(created.Value.IsActive, Is.True);
                    });
    A.CallTo(() => _repository.AddAsync(created.Value, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task CreateAsync_ANameAnotherActiveUnitHoldsInOtherCase_IsRefusedAsNameTaken()
  {
    ErrorOr<Unit> created = await _service.CreateAsync("G", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.units.nameTaken"));
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(created.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("ValidationFailed"));
                    });
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CreateAsync_TheNameOfADeactivatedUnitInOtherCase_IsRefusedNamingThatUnit()
  {
    ErrorOr<Unit> created = await _service.CreateAsync("ML", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.units.nameBelongsToDeactivatedUnit"));
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(created.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("ValidationFailed"));
                      Assert.That(created.RefusalMetadata(Refusal.MetadataKeys.Name), Is.EqualTo("ml"));
                    });
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task UpdateAsync_ItsOwnNameInOtherCase_RenamesTheUnit()
  {
    ErrorOr<Unit> updated = await _service.UpdateAsync(_gram.Id, "G", CancellationToken.None);

    Assert.That(updated.Value.Name, Is.EqualTo("G"));
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task UpdateAsync_ANameAnotherActiveUnitHolds_IsRefusedAsNameTaken()
  {
    ErrorOr<Unit> updated = await _service.UpdateAsync(_gram.Id, "L", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.units.nameTaken"));
                      Assert.That(_gram.Name, Is.EqualTo("g"));
                    });
  }

  [Test]
  public async Task UpdateAsync_TheNameOfADeactivatedUnit_IsRefusedNamingThatUnit()
  {
    ErrorOr<Unit> updated = await _service.UpdateAsync(_gram.Id, "ml", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.units.nameBelongsToDeactivatedUnit"));
                      Assert.That(updated.RefusalMetadata(Refusal.MetadataKeys.Name), Is.EqualTo("ml"));
                      Assert.That(_gram.Name, Is.EqualTo("g"));
                    });
  }

  [Test]
  public async Task UpdateAsync_AnUnknownUnit_IsRefusedAsNotFound()
  {
    ErrorOr<Unit> updated = await _service.UpdateAsync(Guid.NewGuid(), "Portion", CancellationToken.None);

    Assert.That(updated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public async Task ActivateAsync_ASwitchedOffUnit_SwitchesItOn()
  {
    ErrorOr<Unit> activated = await _service.ActivateAsync(_millilitre.Id, CancellationToken.None);

    Assert.That(activated.Value.IsActive, Is.True);
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task ActivateAsync_AnUnknownUnit_IsRefusedAsNotFound()
  {
    ErrorOr<Unit> activated = await _service.ActivateAsync(Guid.NewGuid(), CancellationToken.None);

    Assert.That(activated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public async Task DeactivateAsync_AnUnusedUnit_SwitchesItOff()
  {
    ErrorOr<Unit> deactivated = await _service.DeactivateAsync(_gram.Id, CancellationToken.None);

    Assert.That(deactivated.Value.IsActive, Is.False);
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task DeactivateAsync_AnIngredientUsesTheUnit_IsRefusedNamingTheIngredientsAndStaysSwitchedOn()
  {
    _gram.Ingredients.Add(IngredientNamed("Senf"));
    _gram.Ingredients.Add(IngredientNamed("Zwiebeln"));

    ErrorOr<Unit> deactivated = await _service.DeactivateAsync(_gram.Id, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(deactivated.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(deactivated.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(deactivated.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("UnitUsedByIngredients"));
                      Assert.That(_gram.IsActive, Is.True);
                    });
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task DeactivateAsync_ADeactivatedIngredientUsesTheUnit_IsRefusedNamingThatIngredientAndStaysSwitchedOn()
  {
    _gram.Ingredients.Add(IngredientNamed("Dill", isActive: false));

    ErrorOr<Unit> deactivated = await _service.DeactivateAsync(_gram.Id, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(deactivated.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(deactivated.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(deactivated.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("UnitUsedByIngredients"));
                      Assert.That(deactivated.FirstError.Description, Does.Contain("Dill"));
                      Assert.That(_gram.IsActive, Is.True);
                    });
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task DeactivateAsync_AnUnknownUnit_IsRefusedAsNotFound()
  {
    ErrorOr<Unit> deactivated = await _service.DeactivateAsync(Guid.NewGuid(), CancellationToken.None);

    Assert.That(deactivated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  private Ingredient IngredientNamed(string name, bool isActive = true)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             UnitId = _gram.Id,
             Unit = _gram,
             IsActive = isActive
           };
  }
}
