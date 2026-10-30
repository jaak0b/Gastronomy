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
    _portion = AddUnit("Portion");
    _barrel = AddUnit("Fass");
    _crate = AddUnit("Kiste");
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
  private Unit _crate = null!;
  private UnitService _service = null!;

  [Test]
  public async Task ListAsync_UnitsWithIngredients_ReadsThemByNameWithTheNamesOfTheirIngredientsSortedByName()
  {
    AddIngredient("Senf", _portion);
    AddIngredient("Zwiebeln", _portion);
    AddIngredient("Apfelsaft", _portion);
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
                      Assert.That(ours[0].UsedByIngredientNames, Is.Empty);
                      Assert.That(ours[1].UnitId, Is.EqualTo(_crate.Id));
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
    AddIngredient("Zwiebel", _portion);
    AddIngredient("apfel", _portion);
    AddIngredient("Birne", _portion);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var list = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);
    ErrorOr<Success> deleted = await _service.DeleteAsync(_portion.Id, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(list.Units.Single(unit => unit.UnitId == _portion.Id).UsedByIngredientNames,
                                  Is.EqualTo(new[]
                                             {
                                               "apfel",
                                               "Birne",
                                               "Zwiebel"
                                             }));
                      Assert.That(deleted.RefusalDescription(), Does.Contain("apfel, Birne, Zwiebel"));
                    });
  }

  [Test]
  public async Task CreateAsync_ANewName_StoresTheNameAsTyped()
  {
    ErrorOr<AdminUnitView> created = await _service.CreateAsync(RequestFor("Packung"), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = await _fixture.DbContext.Units.SingleAsync(unit => unit.Name == "Packung", TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.Value.UnitId, Is.EqualTo(stored.Id));
                      Assert.That(created.Value.Name, Is.EqualTo("Packung"));
                      Assert.That(created.Value.UsedByIngredientNames, Is.Empty);
                    });
  }

  [Test]
  public async Task CreateAsync_ANameAnotherUnitHoldsInOtherCase_IsRefusedAsNameTakenAndStoresNothing()
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
  public async Task UpdateAsync_ANameAnotherUnitHolds_IsRefusedAsNameTakenAndKeepsTheName()
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
  public async Task UpdateAsync_AnUnknownUnit_IsRefusedAsNotFound()
  {
    ErrorOr<SavedUnitView> updated = await _service.UpdateAsync(Guid.NewGuid(), RequestFor("Packung"), TestContext.CurrentContext.CancellationToken);

    Assert.That(updated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public async Task DeleteAsync_AnUnusedUnit_DeletesIt()
  {
    var before = StoredUnitCount();

    ErrorOr<Success> deleted = await _service.DeleteAsync(_portion.Id, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(deleted.IsError, Is.False);
                      Assert.That(_fixture.DbContext.Units.Any(unit => unit.Id == _portion.Id), Is.False);
                      Assert.That(StoredUnitCount(), Is.EqualTo(before - 1));
                    });
  }

  [Test]
  public async Task DeleteAsync_AnIngredientUsesTheUnit_IsRefusedNamingTheIngredientsAndKeepsTheUnit()
  {
    AddIngredient("Zwiebeln", _portion);
    AddIngredient("Senf", _portion);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<Success> deleted = await _service.DeleteAsync(_portion.Id, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(deleted.RefusalMessageKey(), Is.EqualTo("common.errors.actionFailed"));
                      Assert.That(deleted.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(deleted.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("UnitUsedByIngredients"));
                      Assert.That(deleted.RefusalDescription(), Does.Contain("Senf, Zwiebeln"));
                      Assert.That(_fixture.DbContext.Units.Any(unit => unit.Id == _portion.Id), Is.True);
                    });
  }

  [Test]
  public async Task DeleteAsync_AnUnknownUnit_IsRefusedAsNotFoundAndDeletesNothing()
  {
    var before = StoredUnitCount();

    ErrorOr<Success> deleted = await _service.DeleteAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(deleted.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(StoredUnitCount(), Is.EqualTo(before));
                    });
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

  private void AddIngredient(string name, Unit unit)
  {
    _fixture.DbContext.Ingredients.Add(new()
                                       {
                                         Id = Guid.NewGuid(),
                                         Name = name,
                                         UnitId = unit.Id,
                                         Unit = unit
                                       });
  }
}
