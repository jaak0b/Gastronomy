using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class CatalogCategoryOrderingTest
{
  [SetUp]
  public void SetUp()
  {
    _ordering = new();
    _first = Guid.NewGuid();
    _middle = Guid.NewGuid();
    _last = Guid.NewGuid();
  }

  private CatalogCategoryOrdering _ordering = null!;
  private Guid _first;
  private Guid _middle;
  private Guid _last;

  [Test]
  public void NextSortOrder_NoCategoryExistsYet_StartsAtTheFirstPosition()
  {
    Assert.That(_ordering.NextSortOrder([]), Is.EqualTo(1));
  }

  [Test]
  public void NextSortOrder_CategoriesExist_TakesThePositionAfterTheLastOne()
  {
    Assert.That(_ordering.NextSortOrder([1, 2, 5]), Is.EqualTo(6));
  }

  [Test]
  public void Move_DownFromTheFirstPosition_SwapsItWithTheOneBelow()
  {
    IReadOnlyList<CatalogCategoryPosition> moved =
      _ordering.Move([_first, _middle, _last], _first, CategoryMoveDirection.Down);

    Assert.That(moved, Is.EqualTo(new CatalogCategoryPosition[] { new(_middle, 1), new(_first, 2), new(_last, 3) }));
  }

  [Test]
  public void Move_UpFromTheLastPosition_SwapsItWithTheOneAbove()
  {
    IReadOnlyList<CatalogCategoryPosition> moved =
      _ordering.Move([_first, _middle, _last], _last, CategoryMoveDirection.Up);

    Assert.That(moved, Is.EqualTo(new CatalogCategoryPosition[] { new(_first, 1), new(_last, 2), new(_middle, 3) }));
  }

  [Test]
  public void Move_UpFromTheFirstPosition_ChangesNothing()
  {
    IReadOnlyList<CatalogCategoryPosition> moved =
      _ordering.Move([_first, _middle, _last], _first, CategoryMoveDirection.Up);

    Assert.That(moved, Is.EqualTo(new CatalogCategoryPosition[] { new(_first, 1), new(_middle, 2), new(_last, 3) }));
  }

  [Test]
  public void Move_DownFromTheLastPosition_ChangesNothing()
  {
    IReadOnlyList<CatalogCategoryPosition> moved =
      _ordering.Move([_first, _middle, _last], _last, CategoryMoveDirection.Down);

    Assert.That(moved, Is.EqualTo(new CatalogCategoryPosition[] { new(_first, 1), new(_middle, 2), new(_last, 3) }));
  }

  [Test]
  public void Move_TheOnlyCategory_ChangesNothing()
  {
    IReadOnlyList<CatalogCategoryPosition> moved = _ordering.Move([_first], _first, CategoryMoveDirection.Down);

    Assert.That(moved, Is.EqualTo(new CatalogCategoryPosition[] { new(_first, 1) }));
  }

  [Test]
  public void Move_CategoryThatIsNotInTheList_ChangesNothing()
  {
    IReadOnlyList<CatalogCategoryPosition> moved = _ordering.Move([_first, _middle], _last, CategoryMoveDirection.Up);

    Assert.That(moved, Is.EqualTo(new CatalogCategoryPosition[] { new(_first, 1), new(_middle, 2) }));
  }

  [Test]
  public void Move_PositionsWithGapsBetweenThem_NumbersThemWithoutGaps()
  {
    IReadOnlyList<CatalogCategoryPosition> moved =
      _ordering.Move([_first, _middle, _last], _middle, CategoryMoveDirection.Up);

    Assert.That(moved.Select(position => position.SortOrder), Is.EqualTo(new[] { 1, 2, 3 }));
  }

  [Test]
  public void Move_NoListOfCategories_IsRefused()
  {
    Assert.That(() => _ordering.Move(null!, _first, CategoryMoveDirection.Up), Throws.ArgumentNullException);
  }
}
