using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class CatalogCategoryOrderingTest
{
  [SetUp]
  public void SetUp()
  {
    _ordering = new();
    _first = BuildCategory("Speisen", 1);
    _middle = BuildCategory("Getraenke", 2);
    _last = BuildCategory("Kuchen", 3);
  }

  private CatalogCategoryOrdering _ordering = null!;
  private CatalogCategory _first = null!;
  private CatalogCategory _middle = null!;
  private CatalogCategory _last = null!;

  [Test]
  public void NextSortOrder_NoCategoryExistsYet_StartsAtTheFirstPosition()
  {
    Assert.That(_ordering.NextSortOrder([]), Is.EqualTo(1));
  }

  [Test]
  public void NextSortOrder_CategoriesExist_TakesThePositionAfterTheLastOne()
  {
    Assert.That(_ordering.NextSortOrder([
                                          1,
                                          2,
                                          5
                                        ]),
                Is.EqualTo(6));
  }

  [Test]
  public void Move_DownFromTheFirstPosition_SwapsItWithTheOneBelow()
  {
    IReadOnlyList<CatalogCategory> moved = _ordering.Move(AllThree(), _first.Id, CategoryMoveDirection.Down);

    Assert.That(moved,
                Is.EqualTo(new[]
                           {
                             _middle,
                             _first,
                             _last
                           }));
  }

  [Test]
  public void Move_UpFromTheLastPosition_SwapsItWithTheOneAbove()
  {
    IReadOnlyList<CatalogCategory> moved = _ordering.Move(AllThree(), _last.Id, CategoryMoveDirection.Up);

    Assert.That(moved,
                Is.EqualTo(new[]
                           {
                             _first,
                             _last,
                             _middle
                           }));
  }

  [Test]
  public void Move_UpFromTheFirstPosition_ChangesNothing()
  {
    IReadOnlyList<CatalogCategory> moved = _ordering.Move(AllThree(), _first.Id, CategoryMoveDirection.Up);

    Assert.That(moved, Is.EqualTo(AllThree()));
  }

  [Test]
  public void Move_DownFromTheLastPosition_ChangesNothing()
  {
    IReadOnlyList<CatalogCategory> moved = _ordering.Move(AllThree(), _last.Id, CategoryMoveDirection.Down);

    Assert.That(moved, Is.EqualTo(AllThree()));
  }

  [Test]
  public void Move_TheOnlyCategory_ChangesNothing()
  {
    IReadOnlyList<CatalogCategory> moved = _ordering.Move([_first], _first.Id, CategoryMoveDirection.Down);

    Assert.That(moved, Is.EqualTo(new[] { _first }));
  }

  [Test]
  public void Move_CategoryThatIsNotInTheList_ChangesNothing()
  {
    IReadOnlyList<CatalogCategory> moved = _ordering.Move([
                                                            _first,
                                                            _middle
                                                          ],
                                                          _last.Id,
                                                          CategoryMoveDirection.Up);

    Assert.That(moved,
                Is.EqualTo(new[]
                           {
                             _first,
                             _middle
                           }));
  }

  [Test]
  public void Move_NoListOfCategories_IsRefused()
  {
    Assert.That(() => _ordering.Move(null!, _first.Id, CategoryMoveDirection.Up), Throws.ArgumentNullException);
  }

  [Test]
  public void IsNumberedInOrder_PositionsWithGapsBetweenThem_AnswersFalse()
  {
    _last.SortOrder = 7;

    Assert.That(_ordering.IsNumberedInOrder(AllThree()), Is.False);
  }

  [Test]
  public void IsNumberedInOrder_PositionsCountingUpFromOne_AnswersTrue()
  {
    Assert.That(_ordering.IsNumberedInOrder(AllThree()), Is.True);
  }

  [Test]
  public void NumberInOrder_PositionsWithGapsBetweenThem_NumbersThemWithoutGaps()
  {
    _first.SortOrder = 4;
    _middle.SortOrder = 9;
    _last.SortOrder = 11;

    _ordering.NumberInOrder(AllThree());

    Assert.That(AllThree().Select(category => category.SortOrder),
                Is.EqualTo(new[]
                           {
                             1,
                             2,
                             3
                           }));
  }

  private IReadOnlyList<CatalogCategory> AllThree()
  {
    return
    [
      _first,
      _middle,
      _last
    ];
  }

  private CatalogCategory BuildCategory(string name, int sortOrder)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             ColourHex = "#C62828",
             SortOrder = sortOrder,
             IsActive = true
           };
  }
}
