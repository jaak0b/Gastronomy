using ErrorOr;
using GastronomyApp.Contracts.Admin.Units;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class UnitServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _portion = AddUnit("Portion", true);
    _barrel = AddUnit("Fass", true);
    _switchedOffCrate = AddUnit("Kiste", false);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _service = new(_fixture.DbContext, new CoreMappingFactory().Create());
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private SqliteInMemoryFixture _fixture = null!;
  private Unit _portion = null!;
  private Unit _barrel = null!;
  private Unit _switchedOffCrate = null!;
  private UnitService _service = null!;

  [Test]
  public async Task ListAsync_UnitsWithIngredients_ReadsThemByNameWithTheNamesOfTheirIngredientsSortedByName()
  {
    AddIngredient("Senf", _portion, true);
    AddIngredient("Zwiebeln", _portion, true);
    AddIngredient("Apfelsaft", _portion, false);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var list = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);

    var ours = list.Units.Where(unit => unit.Name is "Portion" or "Fass" or "Kiste").ToList();
    Assert.Multiple(() =>
                    {
                      Assert.That(ours.Select(unit => unit.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "Fass",
                                               "Kiste",
                                               "Portion"
                                             }));
                      Assert.That(ours[0].UnitId, Is.EqualTo(_barrel.Id));
                      Assert.That(ours[0].IsActive, Is.True);
                      Assert.That(ours[0].UsedByIngredientNames, Is.Empty);
                      Assert.That(ours[1].UnitId, Is.EqualTo(_switchedOffCrate.Id));
                      Assert.That(ours[1].IsActive, Is.False);
                      Assert.That(ours[2].UsedByIngredientNames,
                                  Is.EqualTo(new[]
                                             {
                                               "Apfelsaft",
                                               "Senf",
                                               "Zwiebeln"
                                             }));
                    });
  }

  [Test]
  public async Task ListAsync_IngredientNamesInMixedCase_SortsThemIgnoringCaseInTheListAndInTheRefusal()
  {
    AddIngredient("Zwiebel", _portion, true);
    AddIngredient("apfel", _portion, true);
    AddIngredient("Birne", _portion, true);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var list = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);
    ErrorOr<SavedUnitView> deactivated = await _service.DeactivateAsync(_portion.Id, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(list.Units.Single(unit => unit.UnitId == _portion.Id).UsedByIngredientNames,
                                  Is.EqualTo(new[]
                                             {
                                               "apfel",
                                               "Birne",
                                               "Zwiebel"
                                             }));
                      Assert.That(deactivated.RefusalDescription(), Does.Contain("apfel, Birne, Zwiebel"));
                    });
  }

  [Test]
  public async Task CreateAsync_ANewName_StoresTheNameAsTypedAndSwitchedOn()
  {
    ErrorOr<AdminUnitView> created = await _service.CreateAsync(RequestFor("Packung"), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = await _fixture.DbContext.Units.SingleAsync(unit => unit.Name == "Packung", TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.Value.UnitId, Is.EqualTo(stored.Id));
                      Assert.That(created.Value.Name, Is.EqualTo("Packung"));
                      Assert.That(created.Value.IsActive, Is.True);
                      Assert.That(created.Value.UsedByIngredientNames, Is.Empty);
                      Assert.That(stored.IsActive, Is.True);
                    });
  }

  [Test]
  public async Task CreateAsync_ANameAnotherActiveUnitHoldsInOtherCase_IsRefusedAsNameTakenAndStoresNothing()
  {
    var before = StoredUnitCount();

    ErrorOr<AdminUnitView> created = await _service.CreateAsync(RequestFor("PORTION"), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.units.nameTaken"));
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(created.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("ValidationFailed"));
                      Assert.That(StoredUnitCount(), Is.EqualTo(before));
                    });
  }

  [Test]
  public async Task CreateAsync_TheNameOfADeactivatedUnitInOtherCase_IsRefusedNamingThatUnit()
  {
    var before = StoredUnitCount();

    ErrorOr<AdminUnitView> created = await _service.CreateAsync(RequestFor("KISTE"), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.units.nameBelongsToDeactivatedUnit"));
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(created.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("ValidationFailed"));
                      Assert.That(created.RefusalMetadata(Refusal.MetadataKeys.Name), Is.EqualTo("Kiste"));
                      Assert.That(StoredUnitCount(), Is.EqualTo(before));
                    });
  }

  [Test]
  public async Task UpdateAsync_ItsOwnNameInOtherCase_RenamesTheUnit()
  {
    ErrorOr<SavedUnitView> updated = await _service.UpdateAsync(_portion.Id, RequestFor("PORTION"), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.Value, Is.EqualTo(new SavedUnitView(_portion.Id)));
                      Assert.That(_fixture.DbContext.Units.Single(unit => unit.Id == _portion.Id).Name, Is.EqualTo("PORTION"));
                    });
  }

  [Test]
  public async Task UpdateAsync_ANameAnotherActiveUnitHolds_IsRefusedAsNameTakenAndKeepsTheName()
  {
    ErrorOr<SavedUnitView> updated = await _service.UpdateAsync(_portion.Id, RequestFor("Fass"), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.units.nameTaken"));
                      Assert.That(_fixture.DbContext.Units.Single(unit => unit.Id == _portion.Id).Name, Is.EqualTo("Portion"));
                    });
  }

  [Test]
  public async Task UpdateAsync_TheNameOfADeactivatedUnit_IsRefusedNamingThatUnitAndKeepsTheName()
  {
    ErrorOr<SavedUnitView> updated = await _service.UpdateAsync(_portion.Id, RequestFor("kiste"), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.units.nameBelongsToDeactivatedUnit"));
                      Assert.That(updated.RefusalMetadata(Refusal.MetadataKeys.Name), Is.EqualTo("Kiste"));
                      Assert.That(_fixture.DbContext.Units.Single(unit => unit.Id == _portion.Id).Name, Is.EqualTo("Portion"));
                    });
  }

  [Test]
  public async Task UpdateAsync_AnUnknownUnit_IsRefusedAsNotFound()
  {
    ErrorOr<SavedUnitView> updated = await _service.UpdateAsync(Guid.NewGuid(), RequestFor("Packung"), TestContext.CurrentContext.CancellationToken);

    Assert.That(updated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public async Task DeactivateAsync_AnUnusedUnit_SwitchesItOff()
  {
    ErrorOr<SavedUnitView> deactivated = await _service.DeactivateAsync(_portion.Id, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(deactivated.Value, Is.EqualTo(new SavedUnitView(_portion.Id)));
                      Assert.That(_fixture.DbContext.Units.Single(unit => unit.Id == _portion.Id).IsActive, Is.False);
                    });
  }

  [Test]
  public async Task DeactivateAsync_AnIngredientUsesTheUnit_IsRefusedNamingTheIngredientsAndStaysSwitchedOn()
  {
    AddIngredient("Senf", _portion, true);
    AddIngredient("Zwiebeln", _portion, true);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<SavedUnitView> deactivated = await _service.DeactivateAsync(_portion.Id, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(deactivated.RefusalMessageKey(), Is.EqualTo("common.errors.actionFailed"));
                      Assert.That(deactivated.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(deactivated.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("UnitUsedByIngredients"));
                      Assert.That(deactivated.RefusalDescription(), Does.Contain("Senf, Zwiebeln"));
                      Assert.That(_fixture.DbContext.Units.Single(unit => unit.Id == _portion.Id).IsActive, Is.True);
                    });
  }

  [Test]
  public async Task DeactivateAsync_ADeactivatedIngredientUsesTheUnit_IsRefusedNamingThatIngredientAndStaysSwitchedOn()
  {
    AddIngredient("Dill", _portion, false);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<SavedUnitView> deactivated = await _service.DeactivateAsync(_portion.Id, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(deactivated.RefusalMessageKey(), Is.EqualTo("common.errors.actionFailed"));
                      Assert.That(deactivated.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(deactivated.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("UnitUsedByIngredients"));
                      Assert.That(deactivated.RefusalDescription(), Does.Contain("Dill"));
                      Assert.That(_fixture.DbContext.Units.Single(unit => unit.Id == _portion.Id).IsActive, Is.True);
                    });
  }

  [Test]
  public async Task DeactivateAsync_AnUnknownUnit_IsRefusedAsNotFound()
  {
    ErrorOr<SavedUnitView> deactivated = await _service.DeactivateAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(deactivated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  private SaveUnitRequest RequestFor(string name)
  {
    return new() { Name = name };
  }

  private int StoredUnitCount()
  {
    using var readContext = _fixture.CreateContext();

    return readContext.Units.Count();
  }

  private Unit AddUnit(string name, bool isActive)
  {
    Unit unit = new()
                {
                  Id = Guid.NewGuid(),
                  Name = name,
                  IsActive = isActive
                };
    _fixture.DbContext.Units.Add(unit);

    return unit;
  }

  private void AddIngredient(string name, Unit unit, bool isActive)
  {
    _fixture.DbContext.Ingredients.Add(new()
                                       {
                                         Id = Guid.NewGuid(),
                                         Name = name,
                                         UnitId = unit.Id,
                                         Unit = unit,
                                         IsActive = isActive
                                       });
  }
}
