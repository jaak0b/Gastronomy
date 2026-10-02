using ErrorOr;
using FakeItEasy;
using GastronomyApp.Contracts.Enums;
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
    _mustard = new()
    {
      Id = Guid.NewGuid(),
      Name = "Senf",
      Unit = IngredientUnit.Gram,
      IsActive = true
    };

    A.CallTo(() => _repository.FindAllOrderedByNameAsync(A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult<IReadOnlyList<Ingredient>>([_mustard]));
    A.CallTo(() => _repository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Ingredient?>(null));
    A.CallTo(() => _repository.FindByIdAsync(_mustard.Id, A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult<Ingredient?>(_mustard));
    A.CallTo(() => _festivalRepository.FindAllAsync(A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyCollection<Festival>>([BuildFestival(_summerFestivalId), BuildFestival(_autumnFestivalId)]));

    _service = new(_repository, _festivalRepository);
  }

  private readonly Guid _summerFestivalId = Guid.NewGuid();
  private readonly Guid _autumnFestivalId = Guid.NewGuid();

  private IIngredientRepository _repository = null!;
  private IFestivalRepository _festivalRepository = null!;
  private Ingredient _mustard = null!;
  private IngredientAdministrationService _service = null!;

  [Test]
  public async Task CreateAsync_ANewName_StoresTheNameAsTypedWithAnUnlimitedStockRowPerFestival()
  {
    ErrorOr<Ingredient> created = await _service.CreateAsync("Bratwurst roh", IngredientUnit.Piece, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.Value.Name, Is.EqualTo("Bratwurst roh"));
                      Assert.That(created.Value.Unit, Is.EqualTo(IngredientUnit.Piece));
                      Assert.That(created.Value.IsActive, Is.True);
                      Assert.That(created.Value.FestivalIngredients.Select(stock => stock.FestivalId), Is.EquivalentTo(new[] { _summerFestivalId, _autumnFestivalId }));
                      Assert.That(created.Value.FestivalIngredients.All(stock => stock.IngredientId == created.Value.Id && stock.AvailableAmount == null), Is.True);
                    });
    A.CallTo(() => _repository.AddAsync(created.Value, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task CreateAsync_ANameAnotherIngredientHoldsInOtherCase_IsRefusedAsNameTaken()
  {
    ErrorOr<Ingredient> created = await _service.CreateAsync("SENF", IngredientUnit.Gram, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.nameTaken"));
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(created.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("ValidationFailed"));
                    });
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CreateAsync_AUnitOutsideTheThreeUnits_IsRefusedWithoutStoring()
  {
    ErrorOr<Ingredient> created = await _service.CreateAsync("Ketchup", (IngredientUnit)9, CancellationToken.None);

    Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task UpdateAsync_ItsOwnNameInOtherCaseAndANewUnit_RenamesAndChangesTheUnit()
  {
    ErrorOr<Ingredient> updated = await _service.UpdateAsync(_mustard.Id, "senf", IngredientUnit.Millilitre, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.Value.Name, Is.EqualTo("senf"));
                      Assert.That(updated.Value.Unit, Is.EqualTo(IngredientUnit.Millilitre));
                    });
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task UpdateAsync_ANameAnotherIngredientHolds_IsRefusedAsNameTaken()
  {
    Ingredient ketchup = new()
    {
      Id = Guid.NewGuid(),
      Name = "Ketchup",
      Unit = IngredientUnit.Millilitre,
      IsActive = true
    };
    A.CallTo(() => _repository.FindByIdAsync(ketchup.Id, A<CancellationToken>._)).Returns(Task.FromResult<Ingredient?>(ketchup));

    ErrorOr<Ingredient> updated = await _service.UpdateAsync(ketchup.Id, "Senf", IngredientUnit.Millilitre, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.nameTaken"));
                      Assert.That(ketchup.Name, Is.EqualTo("Ketchup"));
                    });
  }

  [Test]
  public async Task UpdateAsync_AnUnknownIngredient_IsRefusedAsNotFound()
  {
    ErrorOr<Ingredient> updated = await _service.UpdateAsync(Guid.NewGuid(), "Zwiebeln", IngredientUnit.Gram, CancellationToken.None);

    Assert.That(updated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public async Task DeactivateAsync_AnActiveIngredient_SwitchesItOff()
  {
    ErrorOr<Ingredient> deactivated = await _service.DeactivateAsync(_mustard.Id, CancellationToken.None);

    Assert.That(deactivated.Value.IsActive, Is.False);
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
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
