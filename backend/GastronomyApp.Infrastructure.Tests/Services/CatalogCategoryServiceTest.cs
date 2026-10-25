using ErrorOr;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class CatalogCategoryServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _food = AddCategory("Speisen", 1, true);
    _drinks = AddCategory("Getraenke", 2, true);
    _switchedOffCakes = AddCategory("Kuchen", 3, false);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _service = new(_fixture.DbContext, new CoreMappingFactory().Create(), new());
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private SqliteInMemoryFixture _fixture = null!;
  private CatalogCategory _food = null!;
  private CatalogCategory _drinks = null!;
  private CatalogCategory _switchedOffCakes = null!;
  private CatalogCategoryService _service = null!;

  [Test]
  public async Task ListAsync_CategoriesStoredOutOfOrder_ReadsThemBySortOrderWithTheirFields()
  {
    _food.SortOrder = 5;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var list = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(list.Categories.Select(category => category.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "Getraenke",
                                               "Kuchen",
                                               "Speisen"
                                             }));
                      Assert.That(list.Categories[0].CategoryId, Is.EqualTo(_drinks.Id));
                      Assert.That(list.Categories[0].ColourHex, Is.EqualTo("#C62828"));
                      Assert.That(list.Categories[0].SortOrder, Is.EqualTo(2));
                      Assert.That(list.Categories[1].IsActive, Is.False);
                    });
  }

  [Test]
  public async Task CreateAsync_NameOfAnotherCategoryInAnotherCasing_FailsBecauseTheNameIsTakenAndStoresNothing()
  {
    var before = StoredCategoryCount();

    ErrorOr<AdminCategoryView> created = await _service.CreateAsync(RequestFor("speisen"), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.categories.nameTaken"));
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(StoredCategoryCount(), Is.EqualTo(before));
                    });
  }

  [Test]
  public async Task CreateAsync_NameOfASwitchedOffCategory_FailsBecauseTheNameIsTaken()
  {
    ErrorOr<AdminCategoryView> created = await _service.CreateAsync(RequestFor("KUCHEN"), TestContext.CurrentContext.CancellationToken);

    Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.categories.nameTaken"));
  }

  [Test]
  public async Task CreateAsync_ANameNothingRefuses_StoresItBehindTheLastCategoryAndSwitchedOn()
  {
    ErrorOr<AdminCategoryView> created = await _service.CreateAsync(RequestFor("Nachtisch"), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = await _fixture.DbContext.CatalogCategories.SingleAsync(category => category.Name == "Nachtisch", TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsError, Is.False);
                      Assert.That(created.Value.CategoryId, Is.EqualTo(stored.Id));
                      Assert.That(created.Value.Name, Is.EqualTo("Nachtisch"));
                      Assert.That(created.Value.ColourHex, Is.EqualTo("#2E7D32"));
                      Assert.That(created.Value.SortOrder, Is.EqualTo(4));
                      Assert.That(created.Value.IsActive, Is.True);
                      Assert.That(stored.SortOrder, Is.EqualTo(4));
                      Assert.That(stored.IsActive, Is.True);
                    });
  }

  [Test]
  public async Task CreateAsync_NoCategoryYet_StoresItAtTheFirstPosition()
  {
    _fixture.DbContext.CatalogCategories.RemoveRange(_fixture.DbContext.CatalogCategories);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<AdminCategoryView> created = await _service.CreateAsync(RequestFor("Nachtisch"), TestContext.CurrentContext.CancellationToken);

    Assert.That(created.Value.SortOrder, Is.EqualTo(1));
  }

  [Test]
  public async Task UpdateAsync_ItsOwnNameInOtherCase_RenamesTheCategoryAndChangesItsColour()
  {
    ErrorOr<AdminCategoryView> updated = await _service.UpdateAsync(_food.Id, RequestFor("SPEISEN"), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = _fixture.DbContext.CatalogCategories.Single(category => category.Id == _food.Id);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.Value.CategoryId, Is.EqualTo(_food.Id));
                      Assert.That(updated.Value.Name, Is.EqualTo("SPEISEN"));
                      Assert.That(stored.Name, Is.EqualTo("SPEISEN"));
                      Assert.That(stored.ColourHex, Is.EqualTo("#2E7D32"));
                    });
  }

  [Test]
  public async Task UpdateAsync_ANameAnotherCategoryHolds_FailsBecauseTheNameIsTakenAndKeepsTheName()
  {
    ErrorOr<AdminCategoryView> updated = await _service.UpdateAsync(_food.Id, RequestFor("getraenke"), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.categories.nameTaken"));
                      Assert.That(_fixture.DbContext.CatalogCategories.Single(category => category.Id == _food.Id).Name, Is.EqualTo("Speisen"));
                    });
  }

  [Test]
  public async Task UpdateAsync_CategoryThatIsNotThere_FailsBecauseTheCategoryIsNotFound()
  {
    ErrorOr<AdminCategoryView> updated = await _service.UpdateAsync(Guid.NewGuid(), RequestFor("Nachtisch"), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("CategoryNotFound"));
                      Assert.That(updated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                    });
  }

  [Test]
  public async Task MoveAsync_TheLastCategoryDownwards_KeepsTheOrderAndWritesNothing()
  {
    ErrorOr<AdminCategoryListView> moved = await _service.MoveAsync(_switchedOffCakes.Id, MoveDirection.Down, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.IsError, Is.False);
                      Assert.That(moved.Value.Categories.Select(category => category.CategoryId),
                                  Is.EqualTo(new[]
                                             {
                                               _food.Id,
                                               _drinks.Id,
                                               _switchedOffCakes.Id
                                             }));
                      Assert.That(_fixture.DbContext.ChangeTracker.HasChanges(), Is.False);
                    });
  }

  [Test]
  public async Task MoveAsync_TheMiddleCategoryUpwards_PutsItFirstAndStoresTheNewPositions()
  {
    ErrorOr<AdminCategoryListView> moved = await _service.MoveAsync(_drinks.Id, MoveDirection.Up, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.Value.Categories.Select(category => category.CategoryId),
                                  Is.EqualTo(new[]
                                             {
                                               _drinks.Id,
                                               _food.Id,
                                               _switchedOffCakes.Id
                                             }));
                      Assert.That(moved.Value.Categories.Select(category => category.SortOrder),
                                  Is.EqualTo(new[]
                                             {
                                               1,
                                               2,
                                               3
                                             }));
                      Assert.That(_fixture.DbContext.CatalogCategories.OrderBy(category => category.SortOrder).Select(category => category.Id),
                                  Is.EqualTo(new[]
                                             {
                                               _drinks.Id,
                                               _food.Id,
                                               _switchedOffCakes.Id
                                             }));
                    });
  }

  [Test]
  public async Task MoveAsync_CategoryThatIsNotThere_FailsBecauseTheCategoryIsNotFound()
  {
    ErrorOr<AdminCategoryListView> moved = await _service.MoveAsync(Guid.NewGuid(), MoveDirection.Up, TestContext.CurrentContext.CancellationToken);

    Assert.That(moved.RefusalMessageKey(), Is.EqualTo("CategoryNotFound"));
  }

  [Test]
  public async Task DeactivateAsync_CategoryThatStillHoldsActiveItems_FailsBecauseItHoldsActiveItems()
  {
    AddItem("Bratwurst", _food, true);
    AddItem("Pommes", _food, false);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<AdminCategoryView> switchedOff = await _service.DeactivateAsync(_food.Id, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.RefusalMessageKey(), Is.EqualTo("errors.admin.categories.hasActiveItems"));
                      Assert.That(switchedOff.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(_fixture.DbContext.CatalogCategories.Single(category => category.Id == _food.Id).IsActive, Is.True);
                    });
  }

  [Test]
  public async Task DeactivateAsync_CategoryWhoseOnlyItemIsSwitchedOff_SwitchesItOff()
  {
    AddItem("Pommes", _food, false);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<AdminCategoryView> switchedOff = await _service.DeactivateAsync(_food.Id, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.IsError, Is.False);
                      Assert.That(switchedOff.Value.IsActive, Is.False);
                      Assert.That(_fixture.DbContext.CatalogCategories.Single(category => category.Id == _food.Id).IsActive, Is.False);
                    });
  }

  [Test]
  public async Task DeactivateAsync_ActiveItemOfAnotherCategory_DoesNotHoldTheCategoryBack()
  {
    AddItem("Bier", _drinks, true);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<AdminCategoryView> switchedOff = await _service.DeactivateAsync(_food.Id, TestContext.CurrentContext.CancellationToken);

    Assert.That(switchedOff.IsError, Is.False);
  }

  [Test]
  public async Task DeactivateAsync_CategoryThatIsNotThere_FailsBecauseTheCategoryIsNotFound()
  {
    ErrorOr<AdminCategoryView> switchedOff = await _service.DeactivateAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(switchedOff.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  private SaveCategoryRequest RequestFor(string name)
  {
    return new()
           {
             Name = name,
             ColourHex = "#2E7D32"
           };
  }

  private int StoredCategoryCount()
  {
    using var readContext = _fixture.CreateContext();

    return readContext.CatalogCategories.Count();
  }

  private CatalogCategory AddCategory(string name, int sortOrder, bool isActive)
  {
    CatalogCategory category = new()
                               {
                                 Id = Guid.NewGuid(),
                                 Name = name,
                                 ColourHex = "#C62828",
                                 SortOrder = sortOrder,
                                 IsActive = isActive
                               };
    _fixture.DbContext.CatalogCategories.Add(category);

    return category;
  }

  private void AddItem(string name, CatalogCategory category, bool isActive)
  {
    _fixture.DbContext.CatalogItems.Add(new()
                                        {
                                          Id = Guid.NewGuid(),
                                          Name = name,
                                          CategoryId = category.Id,
                                          Category = category,
                                          SortOrder = 1,
                                          IsActive = isActive
                                        });
  }
}
