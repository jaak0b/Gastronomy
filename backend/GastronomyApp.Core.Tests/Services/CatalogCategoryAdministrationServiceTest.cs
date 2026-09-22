using ErrorOr;
using FakeItEasy;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
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

    _catalogAnnouncer = A.Fake<ICatalogChangeAnnouncer>();

    _service = new(_repository, new(), _catalogAnnouncer, new ImmediateAfterCommitActions());
  }

  private readonly Guid _drinkCategoryId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
  private readonly Guid _foodCategoryId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

  private CatalogCategory _drinks = null!;
  private CatalogCategory _food = null!;
  private ICatalogChangeAnnouncer _catalogAnnouncer = null!;
  private ICatalogCategoryRepository _repository = null!;
  private CatalogCategoryAdministrationService _service = null!;

  [Test]
  public async Task MoveAsync_DownFromTheFirstPosition_TellsTheDevicesTheCatalogChanged()
  {
    await _service.MoveAsync(_foodCategoryId, CategoryMoveDirection.Down, CancellationToken.None);

    A.CallTo(() => _catalogAnnouncer.AnnounceCatalogChangedAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task CreateAsync_ANameAnotherCategoryHolds_TellsTheDevicesNothing()
  {
    await _service.CreateAsync("speisen", "#C62828", CancellationToken.None);

    A.CallTo(() => _catalogAnnouncer.AnnounceCatalogChangedAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CreateAsync_NameOfAnotherCategoryInAnotherCasing_FailsBecauseTheNameIsTaken()
  {
    ErrorOr<CatalogCategory> created = await _service.CreateAsync("speisen", "#C62828", CancellationToken.None);

    Assert.That(created.RefusalMessageKey(), Is.EqualTo("admin.categoryNameTaken"));
  }

  [Test]
  public async Task CreateAsync_ANameNothingRefuses_StoresItBehindTheLastCategoryAndCommits()
  {
    ErrorOr<CatalogCategory> created = await _service.CreateAsync("  Nachtisch  ", "#C62828", CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsSuccess, Is.True);
                      Assert.That(created.Value.Name, Is.EqualTo("Nachtisch"));
                      Assert.That(created.Value.SortOrder, Is.EqualTo(3));
                    });

    A.CallTo(() => _repository.AddAsync(created.Value, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task UpdateAsync_CategoryThatIsNotThere_FailsBecauseTheCategoryIsNotFound()
  {
    ErrorOr<CatalogCategory> updated = await _service.UpdateAsync(Guid.NewGuid(), "Nachtisch", "#C62828", CancellationToken.None);

    Assert.That(updated.RefusalMessageKey(), Is.EqualTo("CategoryNotFound"));
  }

  [Test]
  public async Task MoveAsync_TheLastCategoryDownwards_KeepsTheOrderAndWritesNothing()
  {
    ErrorOr<IReadOnlyList<CatalogCategory>> moved = await _service.MoveAsync(_drinkCategoryId, CategoryMoveDirection.Down, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.IsSuccess, Is.True);
                      Assert.That(moved.Value.Select(category => category.Id),
                                  Is.EqualTo(new[]
                                             {
                                               _foodCategoryId,
                                               _drinkCategoryId
                                             }));
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task MoveAsync_TheLastCategoryUpwards_PutsItFirstAndCommits()
  {
    ErrorOr<IReadOnlyList<CatalogCategory>> moved = await _service.MoveAsync(_drinkCategoryId, CategoryMoveDirection.Up, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.Value.Select(category => category.Id),
                                  Is.EqualTo(new[]
                                             {
                                               _drinkCategoryId,
                                               _foodCategoryId
                                             }));
                      Assert.That(_drinks.SortOrder, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task DeactivateAsync_CategoryThatStillHoldsActiveItems_FailsBecauseItHoldsActiveItems()
  {
    A.CallTo(() => _repository.HoldsActiveItemsAsync(_foodCategoryId, A<CancellationToken>._)).Returns(true);

    ErrorOr<CatalogCategory> switchedOff = await _service.DeactivateAsync(_foodCategoryId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.RefusalMessageKey(), Is.EqualTo("admin.categoryHasActiveItems"));
                      Assert.That(_food.IsActive, Is.True);
                    });
  }

  [Test]
  public async Task DeactivateAsync_CategoryWithoutActiveItems_SwitchesItOff()
  {
    ErrorOr<CatalogCategory> switchedOff = await _service.DeactivateAsync(_foodCategoryId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.IsSuccess, Is.True);
                      Assert.That(_food.IsActive, Is.False);
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
