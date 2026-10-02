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
public sealed class CatalogItemRecipeServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<ICatalogItemIngredientRepository>();
    _itemRepository = A.Fake<ICatalogItemRepository>();
    _ingredientRepository = A.Fake<IIngredientRepository>();

    A.CallTo(() => _itemRepository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<CatalogItem?>(null));
    A.CallTo(() => _itemRepository.FindByIdAsync(_itemId, A<CancellationToken>._))
     .Returns(Task.FromResult<CatalogItem?>(new()
                                            {
                                              Id = _itemId,
                                              Name = "Bratwurst",
                                              CategoryId = Guid.NewGuid(),
                                              SortOrder = 1,
                                              IsActive = true
                                            }));
    A.CallTo(() => _ingredientRepository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Ingredient?>(null));
    A.CallTo(() => _ingredientRepository.FindByIdAsync(_ingredientId, A<CancellationToken>._))
     .Returns(Task.FromResult<Ingredient?>(new()
                                           {
                                             Id = _ingredientId,
                                             Name = "Senf",
                                             Unit = IngredientUnit.Gram,
                                             IsActive = true
                                           }));
    A.CallTo(() => _repository.FindAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<CatalogItemIngredient?>(null));

    _service = new(_repository, _itemRepository, _ingredientRepository);
  }

  private readonly Guid _itemId = Guid.NewGuid();
  private readonly Guid _ingredientId = Guid.NewGuid();

  private ICatalogItemIngredientRepository _repository = null!;
  private ICatalogItemRepository _itemRepository = null!;
  private IIngredientRepository _ingredientRepository = null!;
  private CatalogItemRecipeService _service = null!;

  [Test]
  public async Task SetAmountAsync_NoLineYet_AddsTheLineWithTheAmount()
  {
    ErrorOr<CatalogItemIngredient> saved = await _service.SetAmountAsync(_itemId, _ingredientId, 12.5, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(saved.Value.CatalogItemId, Is.EqualTo(_itemId));
                      Assert.That(saved.Value.IngredientId, Is.EqualTo(_ingredientId));
                      Assert.That(saved.Value.Amount, Is.EqualTo(12.5));
                    });
    A.CallTo(() => _repository.AddAsync(saved.Value, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task SetAmountAsync_ALineExists_ChangesItsAmountWithoutAddingAnother()
  {
    CatalogItemIngredient existing = new()
    {
      Id = Guid.NewGuid(),
      CatalogItemId = _itemId,
      IngredientId = _ingredientId,
      Amount = 10
    };
    A.CallTo(() => _repository.FindAsync(_itemId, _ingredientId, A<CancellationToken>._)).Returns(Task.FromResult<CatalogItemIngredient?>(existing));

    ErrorOr<CatalogItemIngredient> saved = await _service.SetAmountAsync(_itemId, _ingredientId, 15, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(saved.Value, Is.SameAs(existing));
                      Assert.That(existing.Amount, Is.EqualTo(15));
                    });
    A.CallTo(() => _repository.AddAsync(A<CatalogItemIngredient>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [TestCase(0d)]
  [TestCase(-1d)]
  [TestCase(double.NaN)]
  [TestCase(double.PositiveInfinity)]
  public async Task SetAmountAsync_AnAmountThatIsNotANumberAboveZero_IsRefusedAsAmountInvalid(double amount)
  {
    ErrorOr<CatalogItemIngredient> saved = await _service.SetAmountAsync(_itemId, _ingredientId, amount, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(saved.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.amountInvalid"));
                      Assert.That(saved.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(saved.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("ValidationFailed"));
                    });
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task SetAmountAsync_NoAmount_IsRefusedAsAmountInvalid()
  {
    ErrorOr<CatalogItemIngredient> saved = await _service.SetAmountAsync(_itemId, _ingredientId, null, CancellationToken.None);

    Assert.That(saved.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.amountInvalid"));
  }

  [Test]
  public async Task SetAmountAsync_AnUnknownArticle_IsRefusedAsNotFound()
  {
    ErrorOr<CatalogItemIngredient> saved = await _service.SetAmountAsync(Guid.NewGuid(), _ingredientId, 1, CancellationToken.None);

    Assert.That(saved.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public async Task SetAmountAsync_AnUnknownIngredient_IsRefusedAsNotFound()
  {
    ErrorOr<CatalogItemIngredient> saved = await _service.SetAmountAsync(_itemId, Guid.NewGuid(), 1, CancellationToken.None);

    Assert.That(saved.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public async Task RemoveAsync_ALineExists_RemovesIt()
  {
    CatalogItemIngredient existing = new()
    {
      Id = Guid.NewGuid(),
      CatalogItemId = _itemId,
      IngredientId = _ingredientId,
      Amount = 10
    };
    A.CallTo(() => _repository.FindAsync(_itemId, _ingredientId, A<CancellationToken>._)).Returns(Task.FromResult<CatalogItemIngredient?>(existing));

    ErrorOr<CatalogItemIngredient> removed = await _service.RemoveAsync(_itemId, _ingredientId, CancellationToken.None);

    Assert.That(removed.IsError, Is.False);
    A.CallTo(() => _repository.Remove(existing)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RemoveAsync_NoSuchLine_IsRefusedAsNotFound()
  {
    ErrorOr<CatalogItemIngredient> removed = await _service.RemoveAsync(_itemId, _ingredientId, CancellationToken.None);

    Assert.That(removed.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }
}
