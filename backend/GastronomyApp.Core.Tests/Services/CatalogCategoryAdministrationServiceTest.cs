using FakeItEasy;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class CatalogCategoryAdministrationServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<ICatalogCategoryRepository>();
    _transactionRunner = new();
    _food = BuildCategory(_foodCategoryId, "Speisen", 1, true);
    _drinks = BuildCategory(_drinkCategoryId, "Getraenke", 2, true);

    A.CallTo(() => _repository.FindAllOrderedAsync(A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyList<CatalogCategory>>([
                                                              _food,
                                                              _drinks
                                                            ]));
    A.CallTo(() => _repository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<CatalogCategory?>(null));
    A.CallTo(() => _repository.FindByIdAsync(_foodCategoryId, A<CancellationToken>._)).Returns(Task.FromResult<CatalogCategory?>(_food));
    A.CallTo(() => _repository.HoldsActiveItemsAsync(A<Guid>._, A<CancellationToken>._)).Returns(false);

    _service = new(_repository, new(), new(), _transactionRunner);
  }

  private readonly Guid _drinkCategoryId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
  private readonly Guid _foodCategoryId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

  private CatalogCategory _drinks = null!;
  private CatalogCategory _food = null!;
  private ICatalogCategoryRepository _repository = null!;
  private CatalogCategoryAdministrationService _service = null!;
  private RecordingTransactionRunner _transactionRunner = null!;

  [Test]
  public async Task CreateAsync_NameOfOnlySpaces_FailsBecauseTheNameIsMissing()
  {
    Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>> created = await _service.CreateAsync("  ", "#C62828", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.Failure.Reason, Is.EqualTo(CatalogCategoryAdministrationFailureReason.NameMissing));
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });
  }

  [Test]
  public async Task CreateAsync_ColourThatIsNotSixHexDigits_FailsBecauseTheColourIsInvalid()
  {
    Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>> created = await _service.CreateAsync("Nachtisch", "C62828", CancellationToken.None);

    Assert.That(created.Failure.Reason, Is.EqualTo(CatalogCategoryAdministrationFailureReason.ColourInvalid));
  }

  [Test]
  public async Task CreateAsync_NameOfAnotherCategoryInAnotherCasing_FailsBecauseTheNameIsTaken()
  {
    Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>> created = await _service.CreateAsync("speisen", "#C62828", CancellationToken.None);

    Assert.That(created.Failure.Reason, Is.EqualTo(CatalogCategoryAdministrationFailureReason.NameTaken));
  }

  [Test]
  public async Task CreateAsync_ANameNothingRefuses_StoresItBehindTheLastCategoryAndCommits()
  {
    Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>> created = await _service.CreateAsync("  Nachtisch  ", "#C62828", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsSuccess, Is.True);
                      Assert.That(created.Value.Name, Is.EqualTo("Nachtisch"));
                      Assert.That(created.Value.SortOrder, Is.EqualTo(3));
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });

    A.CallTo(() => _repository.AddAsync(created.Value, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task UpdateAsync_CategoryThatIsNotThere_FailsBecauseTheCategoryIsNotFound()
  {
    Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>> updated = await _service.UpdateAsync(Guid.NewGuid(), "Nachtisch", "#C62828", CancellationToken.None);

    Assert.That(updated.Failure.Reason, Is.EqualTo(CatalogCategoryAdministrationFailureReason.CategoryNotFound));
  }

  [Test]
  public async Task MoveAsync_TheLastCategoryDownwards_KeepsTheOrderAndCommitsNothing()
  {
    Result<IReadOnlyList<CatalogCategory>?, Failure<CatalogCategoryAdministrationFailureReason>> moved = await _service.MoveAsync(_drinkCategoryId, CategoryMoveDirection.Down, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.Value, Is.Null);
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task MoveAsync_TheLastCategoryUpwards_PutsItFirstAndCommits()
  {
    Result<IReadOnlyList<CatalogCategory>?, Failure<CatalogCategoryAdministrationFailureReason>> moved = await _service.MoveAsync(_drinkCategoryId, CategoryMoveDirection.Up, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.Value!.Select(category => category.Id),
                                  Is.EqualTo(new[]
                                             {
                                               _drinkCategoryId,
                                               _foodCategoryId
                                             }));
                      Assert.That(_drinks.SortOrder, Is.EqualTo(1));
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });
  }

  [Test]
  public async Task DeactivateAsync_CategoryThatStillHoldsActiveItems_FailsBecauseItHoldsActiveItems()
  {
    A.CallTo(() => _repository.HoldsActiveItemsAsync(_foodCategoryId, A<CancellationToken>._)).Returns(true);

    Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>> switchedOff = await _service.DeactivateAsync(_foodCategoryId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.Failure.Reason, Is.EqualTo(CatalogCategoryAdministrationFailureReason.CategoryHoldsActiveItems));
                      Assert.That(_food.IsActive, Is.True);
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });
  }

  [Test]
  public async Task DeactivateAsync_CategoryWithoutActiveItems_SwitchesItOff()
  {
    Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>> switchedOff = await _service.DeactivateAsync(_foodCategoryId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.IsSuccess, Is.True);
                      Assert.That(_food.IsActive, Is.False);
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });
  }

  private CatalogCategory BuildCategory(Guid categoryId, string name, int sortOrder, bool isActive)
  {
    return new()
           {
             Id = categoryId,
             Name = name,
             ColourHex = "#C62828",
             SortOrder = sortOrder,
             IsActive = isActive
           };
  }
}
