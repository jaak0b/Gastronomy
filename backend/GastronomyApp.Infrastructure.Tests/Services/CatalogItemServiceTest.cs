using ErrorOr;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class CatalogItemServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _switchedOffCategory = new()
                           {
                             Id = Guid.NewGuid(),
                             Name = "Kuchen",
                             ColourHex = "#2E7D32",
                             SortOrder = 3,
                             IsActive = false
                           };
    _gram = new()
            {
              Id = Guid.NewGuid(),
              Name = "Portion",
              IsActive = true
            };
    _fixture.DbContext.CatalogCategories.Add(_switchedOffCategory);
    _fixture.DbContext.Units.Add(_gram);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    FestivalRepository festivalRepository = new(_fixture.DbContext, new());
    _service = new(_fixture.DbContext, new CoreMappingFactory().Create(), new(festivalRepository, new(), TimeProvider.System));
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private SqliteInMemoryFixture _fixture = null!;
  private SeededDomain _seeded = null!;
  private CatalogCategory _switchedOffCategory = null!;
  private Unit _gram = null!;
  private CatalogItemService _service = null!;

  [Test]
  public async Task ListAsync_NoFestival_ReadsEveryArticleBySortOrderWithoutAMenuEntry()
  {
    var list = await _service.ListAsync(null, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(list.Value.Items.Select(item => item.ItemId),
                                  Is.EqualTo(new[]
                                             {
                                               _seeded.SausageItemId,
                                               _seeded.LemonadeItemId
                                             }));
                      Assert.That(list.Value.Items[0].Name, Is.EqualTo("Bratwurst"));
                      Assert.That(list.Value.Items[0].CategoryId, Is.EqualTo(_seeded.FoodCategoryId));
                      Assert.That(list.Value.Items.All(item => item.AtTheFestival is null), Is.True);
                    });
  }

  [Test]
  public async Task ListAsync_AFestival_CarriesThePriceAvailabilityAndStationsOfThatFestivalOnly()
  {
    var lastYearId = Guid.NewGuid();
    _fixture.DbContext.Festivals.Add(new()
                                     {
                                       Id = lastYearId,
                                       Name = "Sommerfest im letzten Jahr",
                                       StartsAtUtc = DateTime.UtcNow.AddYears(-1),
                                       EndsAtUtc = DateTime.UtcNow.AddYears(-1).AddDays(2),
                                       NextOrderNumber = 1,
                                       IsHidden = false
                                     });
    _fixture.DbContext.FestivalCatalogItems.Add(new()
                                                {
                                                  Id = Guid.NewGuid(),
                                                  FestivalId = lastYearId,
                                                  CatalogItemId = _seeded.SausageItemId,
                                                  PriceCents = 999,
                                                  IsAvailable = false
                                                });
    _fixture.DbContext.ItemStationAssignments.Add(new()
                                                  {
                                                    Id = Guid.NewGuid(),
                                                    FestivalId = lastYearId,
                                                    CatalogItemId = _seeded.SausageItemId,
                                                    StationId = _seeded.BarStationId
                                                  });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var list = await _service.ListAsync(_seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    var sausage = list.Value.Items.Single(item => item.ItemId == _seeded.SausageItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(sausage.AtTheFestival!.PriceCents, Is.EqualTo(350));
                      Assert.That(sausage.AtTheFestival.IsAvailable, Is.True);
                      Assert.That(sausage.AtTheFestival.StationIds, Is.EqualTo(new[] { _seeded.KitchenStationId }));
                    });
  }

  [Test]
  public async Task ListAsync_AFestivalWhereAnArticleIsNotOnTheMenu_CarriesNoMenuEntryForIt()
  {
    var otherFestivalId = Guid.NewGuid();
    _fixture.DbContext.Festivals.Add(new()
                                     {
                                       Id = otherFestivalId,
                                       Name = "Herbstfest",
                                       StartsAtUtc = DateTime.UtcNow.AddDays(90),
                                       EndsAtUtc = DateTime.UtcNow.AddDays(91),
                                       NextOrderNumber = 1,
                                       IsHidden = false
                                     });
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var list = await _service.ListAsync(otherFestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(list.Value.Items.All(item => item.AtTheFestival is null), Is.True);
  }

  [Test]
  public async Task ListAsync_FestivalThatIsNotThere_FailsBecauseTheFestivalIsNotFound()
  {
    var list = await _service.ListAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(list.IsError, Is.True);
                      Assert.That(list.RefusalMessageKey(), Is.EqualTo("FestivalNotFound"));
                    });
  }

  [Test]
  public async Task ListAsync_ARecipeOfTwoIngredients_CarriesTheNameAndUnitOfEveryLineByIngredientName()
  {
    var piece = AddUnit("Fass");
    var mustard = AddIngredient("Senf", _gram, true);
    var bun = AddIngredient("Brötchen", piece, true);
    AddRecipeLine(_seeded.SausageItemId, mustard, 20);
    AddRecipeLine(_seeded.SausageItemId, bun, 1);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var list = await _service.ListAsync(null, TestContext.CurrentContext.CancellationToken);

    var sausage = list.Value.Items.Single(item => item.ItemId == _seeded.SausageItemId);

    Assert.That(sausage.Ingredients,
                Is.EqualTo(new[]
                           {
                             new AdminItemIngredientView(bun.Id, "Brötchen", piece.Id, "Fass", 1),
                             new AdminItemIngredientView(mustard.Id, "Senf", _gram.Id, "Portion", 20)
                           }));
  }

  [Test]
  public async Task ListAsync_IngredientNamesInMixedCase_ComeBackWithCapitalsIgnoredAndUmlautsAfterZ()
  {
    foreach (var name in new[]
                         {
                           "Zwiebel",
                           "Äpfel",
                           "apfel",
                           "Birne"
                         })
      AddRecipeLine(_seeded.SausageItemId, AddIngredient(name, _gram, true), 1);

    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var list = await _service.ListAsync(null, TestContext.CurrentContext.CancellationToken);

    Assert.That(list.Value.Items.Single(item => item.ItemId == _seeded.SausageItemId).Ingredients.Select(ingredient => ingredient.Name),
                Is.EqualTo(new[]
                           {
                             "apfel",
                             "Birne",
                             "Zwiebel",
                             "Äpfel"
                           }));
  }

  [Test]
  public async Task CreateAsync_NameOfAnotherItemInOtherCase_FailsBecauseTheNameIsTakenAndStoresNothing()
  {
    ErrorOr<AdminItemView> created = await _service.CreateAsync(RequestFor("BRATWURST", _seeded.FoodCategoryId), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.items.nameTaken"));
                      Assert.That(_fixture.DbContext.CatalogItems.Count(), Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task CreateAsync_CategoryThatIsNotThere_FailsBecauseTheCategoryIsUnknown()
  {
    ErrorOr<AdminItemView> created = await _service.CreateAsync(RequestFor("Currywurst", Guid.NewGuid()), TestContext.CurrentContext.CancellationToken);

    Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.items.categoryUnknown"));
  }

  [Test]
  public async Task CreateAsync_NoCategory_FailsBecauseTheCategoryIsUnknown()
  {
    ErrorOr<AdminItemView> created = await _service.CreateAsync(RequestFor("Currywurst", null), TestContext.CurrentContext.CancellationToken);

    Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.items.categoryUnknown"));
  }

  [Test]
  public async Task CreateAsync_CategoryThatIsSwitchedOff_FailsBecauseTheCategoryIsSwitchedOff()
  {
    ErrorOr<AdminItemView> created = await _service.CreateAsync(RequestFor("Currywurst", _switchedOffCategory.Id), TestContext.CurrentContext.CancellationToken);

    Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.items.categoryIsOff"));
  }

  [TestCase(-1d)]
  [TestCase(601d)]
  [TestCase(1.25d)]
  public async Task CreateAsync_PreparationTimeTheItemFormNeverProduces_FailsBecauseItIsOutOfRange(double minutes)
  {
    ErrorOr<AdminItemView> created = await _service.CreateAsync(RequestFor("Currywurst", _seeded.FoodCategoryId, minutes), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.items.productionMinutesOutOfRange"));
                      Assert.That(created.RefusalDescription(), Does.Contain(minutes.ToString()));
                    });
  }

  [Test]
  public async Task CreateAsync_AnItemNothingRefuses_StoresItAndHandsBackTheCreatedArticle()
  {
    ErrorOr<AdminItemView> created = await _service.CreateAsync(RequestFor("Currywurst", _seeded.FoodCategoryId, 1.5, 4, true), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = _fixture.DbContext.CatalogItems.Single(item => item.Name == "Currywurst");

    Assert.Multiple(() =>
                    {
                      Assert.That(created.Value.ItemId, Is.EqualTo(stored.Id));
                      Assert.That(created.Value.Name, Is.EqualTo("Currywurst"));
                      Assert.That(created.Value.CategoryId, Is.EqualTo(_seeded.FoodCategoryId));
                      Assert.That(created.Value.SortOrder, Is.EqualTo(4));
                      Assert.That(created.Value.IsActive, Is.True);
                      Assert.That(created.Value.ProductionMinutes, Is.EqualTo(1.5));
                      Assert.That(created.Value.IsQueueIndependent, Is.True);
                      Assert.That(created.Value.AtTheFestival, Is.Null);
                      Assert.That(created.Value.Ingredients, Is.Empty);
                      Assert.That(stored.IsActive, Is.True);
                      Assert.That(stored.ProductionMinutes, Is.EqualTo(1.5));
                      Assert.That(stored.SortOrder, Is.EqualTo(4));
                    });
  }

  [Test]
  public async Task CreateAsync_NameWithSpacesAround_StoresTheNameExactlyAsGiven()
  {
    await _service.CreateAsync(RequestFor(" Currywurst ", _seeded.FoodCategoryId), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.That(_fixture.DbContext.CatalogItems.Select(item => item.Name), Does.Contain(" Currywurst "));
  }

  [Test]
  public async Task UpdateAsync_ItemThatIsNotThere_FailsBecauseTheItemIsNotFound()
  {
    ErrorOr<SavedItemView> updated = await _service.UpdateAsync(Guid.NewGuid(), RequestFor("Currywurst", _seeded.FoodCategoryId), TestContext.CurrentContext.CancellationToken);

    Assert.That(updated.RefusalMessageKey(), Is.EqualTo("ItemNotFound"));
  }

  [Test]
  public async Task UpdateAsync_ItsOwnNameInOtherCase_StoresEveryFieldAndAnswersWithTheItemId()
  {
    ErrorOr<SavedItemView> updated = await _service.UpdateAsync(_seeded.SausageItemId, RequestFor("BRATWURST", _seeded.DrinkCategoryId, 3, 7, true), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = _fixture.DbContext.CatalogItems.Single(item => item.Id == _seeded.SausageItemId);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.Value.ItemId, Is.EqualTo(_seeded.SausageItemId));
                      Assert.That(stored.Name, Is.EqualTo("BRATWURST"));
                      Assert.That(stored.CategoryId, Is.EqualTo(_seeded.DrinkCategoryId));
                      Assert.That(stored.ProductionMinutes, Is.EqualTo(3));
                      Assert.That(stored.SortOrder, Is.EqualTo(7));
                      Assert.That(stored.IsQueueIndependent, Is.True);
                    });
  }

  [Test]
  public async Task UpdateAsync_NameOfAnotherItem_FailsBecauseTheNameIsTakenAndKeepsTheName()
  {
    ErrorOr<SavedItemView> updated = await _service.UpdateAsync(_seeded.SausageItemId, RequestFor("limonade", _seeded.FoodCategoryId), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.items.nameTaken"));
                      Assert.That(_fixture.DbContext.CatalogItems.Single(item => item.Id == _seeded.SausageItemId).Name, Is.EqualTo("Bratwurst"));
                    });
  }

  [Test]
  public async Task UpdateAsync_ASwitchedOffCategoryForAnActiveItem_FailsBecauseTheCategoryIsSwitchedOff()
  {
    ErrorOr<SavedItemView> updated = await _service.UpdateAsync(_seeded.SausageItemId, RequestFor("Bratwurst", _switchedOffCategory.Id), TestContext.CurrentContext.CancellationToken);

    Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.items.categoryIsOff"));
  }

  [Test]
  public async Task UpdateAsync_ASwitchedOffCategoryForASwitchedOffItem_StoresIt()
  {
    var item = _fixture.DbContext.CatalogItems.Single(row => row.Id == _seeded.SausageItemId);
    item.IsActive = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<SavedItemView> updated = await _service.UpdateAsync(_seeded.SausageItemId, RequestFor("Bratwurst", _switchedOffCategory.Id), TestContext.CurrentContext.CancellationToken);

    Assert.That(updated.IsError, Is.False);
  }

  [Test]
  public async Task UpdateAsync_PreparationTimeOutOfRange_FailsAndKeepsTheItem()
  {
    ErrorOr<SavedItemView> updated = await _service.UpdateAsync(_seeded.SausageItemId, RequestFor("Bratwurst", _seeded.FoodCategoryId, 601), TestContext.CurrentContext.CancellationToken);

    Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.items.productionMinutesOutOfRange"));
  }

  [Test]
  public async Task ActivateAsync_ItemThatIsNotThere_FailsBecauseTheItemIsNotFound()
  {
    ErrorOr<SavedItemView> switchedOn = await _service.ActivateAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(switchedOn.RefusalMessageKey(), Is.EqualTo("ItemNotFound"));
  }

  [Test]
  public async Task ActivateAsync_ItemWhoseCategoryIsSwitchedOff_FailsBecauseTheCategoryIsSwitchedOff()
  {
    var pommes = AddSwitchedOffArticle(_switchedOffCategory.Id);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<SavedItemView> switchedOn = await _service.ActivateAsync(pommes.Id, TestContext.CurrentContext.CancellationToken);

    Assert.That(switchedOn.RefusalMessageKey(), Is.EqualTo("errors.admin.items.categoryIsOff"));
  }

  [Test]
  public async Task ActivateAsync_TwoDeactivatedIngredientsInTheRecipe_FailsOncePerIngredientOrderedByNameAndStaysOff()
  {
    var currywurst = await GivenASwitchedOffArticleWithTheRecipeAsync(_seeded.FoodCategoryId, ("Zwiebeln", false), ("Senf", true), ("Brötchen", false));

    ErrorOr<SavedItemView> switchedOn = await _service.ActivateAsync(currywurst.Id, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOn.RefusedLines().Select(line => line.Code),
                                  Is.EqualTo(new[]
                                             {
                                               "errors.admin.items.ingredientIsDeactivated",
                                               "errors.admin.items.ingredientIsDeactivated"
                                             }));
                      Assert.That(switchedOn.RefusedLines().Select(line => line.Metadata![Refusal.MetadataKeys.Name]),
                                  Is.EqualTo(new[]
                                             {
                                               "Brötchen",
                                               "Zwiebeln"
                                             }));
                      Assert.That(switchedOn.FirstError.NumericType, Is.EqualTo(RefusalType.UnprocessableEntity));
                      Assert.That(switchedOn.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("UnprocessableEntity"));
                      Assert.That(_fixture.DbContext.CatalogItems.Single(item => item.Id == currywurst.Id).IsActive, Is.False);
                    });
  }

  [Test]
  public async Task ActivateAsync_DeactivatedIngredientsInMixedCase_AreNamedWithCapitalsIgnoredAndUmlautsAfterZ()
  {
    var currywurst = await GivenASwitchedOffArticleWithTheRecipeAsync(_seeded.FoodCategoryId, ("Zwiebel", false), ("Äpfel", false), ("apfel", false), ("Birne", false));

    ErrorOr<SavedItemView> switchedOn = await _service.ActivateAsync(currywurst.Id, TestContext.CurrentContext.CancellationToken);

    Assert.That(switchedOn.RefusedLines().Select(line => line.Metadata![Refusal.MetadataKeys.Name]),
                Is.EqualTo(new[]
                           {
                             "apfel",
                             "Birne",
                             "Zwiebel",
                             "Äpfel"
                           }));
  }

  [Test]
  public async Task ActivateAsync_ASwitchedOffCategoryAndADeactivatedIngredient_FailsBecauseOfTheCategoryFirst()
  {
    var currywurst = await GivenASwitchedOffArticleWithTheRecipeAsync(_switchedOffCategory.Id, ("Zwiebeln", false));

    ErrorOr<SavedItemView> switchedOn = await _service.ActivateAsync(currywurst.Id, TestContext.CurrentContext.CancellationToken);

    Assert.That(switchedOn.RefusalMessageKey(), Is.EqualTo("errors.admin.items.categoryIsOff"));
  }

  [Test]
  public async Task ActivateAsync_ARecipeOfActiveIngredients_SwitchesTheArticleOn()
  {
    var currywurst = await GivenASwitchedOffArticleWithTheRecipeAsync(_seeded.FoodCategoryId, ("Senf", true));

    ErrorOr<SavedItemView> switchedOn = await _service.ActivateAsync(currywurst.Id, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOn.Value.ItemId, Is.EqualTo(currywurst.Id));
                      Assert.That(_fixture.DbContext.CatalogItems.Single(item => item.Id == currywurst.Id).IsActive, Is.True);
                    });
  }

  [Test]
  public async Task DeactivateAsync_ItemThatIsNotThere_FailsBecauseTheItemIsNotFound()
  {
    ErrorOr<SavedItemView> switchedOff = await _service.DeactivateAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(switchedOff.RefusalMessageKey(), Is.EqualTo("ItemNotFound"));
  }

  [Test]
  public async Task DeactivateAsync_ItemOnTheMenuOfTheRunningFestival_FailsBecauseTheFestivalIsServingIt()
  {
    ErrorOr<SavedItemView> switchedOff = await _service.DeactivateAsync(_seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.RefusalMessageKey(), Is.EqualTo("errors.admin.items.isOnTheRunningFestivalsMenu"));
                      Assert.That(_fixture.DbContext.CatalogItems.Single(item => item.Id == _seeded.SausageItemId).IsActive, Is.True);
                    });
  }

  [Test]
  public async Task DeactivateAsync_ItemNotOnTheMenuOfTheRunningFestival_SwitchesTheItemOff()
  {
    var pommes = AddSwitchedOffArticle(_seeded.FoodCategoryId);
    pommes.IsActive = true;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<SavedItemView> switchedOff = await _service.DeactivateAsync(pommes.Id, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.Value.ItemId, Is.EqualTo(pommes.Id));
                      Assert.That(_fixture.DbContext.CatalogItems.Single(item => item.Id == pommes.Id).IsActive, Is.False);
                    });
  }

  [Test]
  public async Task DeactivateAsync_NoFestivalIsRunning_SwitchesTheItemOff()
  {
    var festival = _fixture.DbContext.Festivals.Single(row => row.Id == _seeded.FestivalId);
    festival.EndsAtUtc = DateTime.UtcNow.AddHours(-1);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<SavedItemView> switchedOff = await _service.DeactivateAsync(_seeded.SausageItemId, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.IsError, Is.False);
                      Assert.That(_fixture.DbContext.CatalogItems.Single(item => item.Id == _seeded.SausageItemId).IsActive, Is.False);
                    });
  }

  [Test]
  public async Task SetRecipeLineAsync_NoLineYet_AddsTheLineWithTheAmount()
  {
    var mustard = await AddStoredIngredientAsync("Senf", true);

    ErrorOr<SavedItemView> saved = await _service.SetRecipeLineAsync(_seeded.SausageItemId, mustard.Id, AmountRequest(12.5), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = _fixture.DbContext.CatalogItemIngredients.Single();

    Assert.Multiple(() =>
                    {
                      Assert.That(saved.Value.ItemId, Is.EqualTo(_seeded.SausageItemId));
                      Assert.That(stored.CatalogItemId, Is.EqualTo(_seeded.SausageItemId));
                      Assert.That(stored.IngredientId, Is.EqualTo(mustard.Id));
                      Assert.That(stored.Amount, Is.EqualTo(12.5));
                    });
  }

  [Test]
  public async Task SetRecipeLineAsync_ALineExists_ChangesItsAmountWithoutAddingAnother()
  {
    var mustard = await AddStoredIngredientAsync("Senf", true);
    AddRecipeLine(_seeded.SausageItemId, mustard, 10);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await _service.SetRecipeLineAsync(_seeded.SausageItemId, mustard.Id, AmountRequest(15), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.That(_fixture.DbContext.CatalogItemIngredients.Single().Amount, Is.EqualTo(15));
  }

  [Test]
  public async Task SetRecipeLineAsync_ADeactivatedIngredientWithoutALineYet_IsRefusedWithoutAddingIt()
  {
    var onions = await AddStoredIngredientAsync("Zwiebeln", false);

    ErrorOr<SavedItemView> saved = await _service.SetRecipeLineAsync(_seeded.SausageItemId, onions.Id, AmountRequest(12.5), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(saved.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(saved.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(saved.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("DeactivatedIngredientCannotJoinRecipe"));
                      Assert.That(_fixture.DbContext.CatalogItemIngredients.Count(), Is.Zero);
                    });
  }

  [Test]
  public async Task SetRecipeLineAsync_ADeactivatedIngredientWhoseLineExists_ChangesTheAmount()
  {
    var onions = await AddStoredIngredientAsync("Zwiebeln", false);
    AddRecipeLine(_seeded.SausageItemId, onions, 10);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<SavedItemView> saved = await _service.SetRecipeLineAsync(_seeded.SausageItemId, onions.Id, AmountRequest(15), TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(saved.IsError, Is.False);
                      Assert.That(_fixture.DbContext.CatalogItemIngredients.Single().Amount, Is.EqualTo(15));
                    });
  }

  [TestCase(0d)]
  [TestCase(-1d)]
  [TestCase(double.NaN)]
  [TestCase(double.PositiveInfinity)]
  public async Task SetRecipeLineAsync_AnAmountThatIsNotANumberAboveZero_IsRefusedAsAmountInvalid(double amount)
  {
    var mustard = await AddStoredIngredientAsync("Senf", true);

    ErrorOr<SavedItemView> saved = await _service.SetRecipeLineAsync(_seeded.SausageItemId, mustard.Id, AmountRequest(amount), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(saved.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.amountInvalid"));
                      Assert.That(saved.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(saved.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("ValidationFailed"));
                      Assert.That(_fixture.DbContext.CatalogItemIngredients.Count(), Is.Zero);
                    });
  }

  [Test]
  public async Task SetRecipeLineAsync_NoAmount_IsRefusedAsAmountInvalid()
  {
    var mustard = await AddStoredIngredientAsync("Senf", true);

    ErrorOr<SavedItemView> saved = await _service.SetRecipeLineAsync(_seeded.SausageItemId, mustard.Id, AmountRequest(null), TestContext.CurrentContext.CancellationToken);

    Assert.That(saved.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.amountInvalid"));
  }

  [Test]
  public async Task SetRecipeLineAsync_AnUnknownArticle_IsRefusedAsNotFound()
  {
    var mustard = await AddStoredIngredientAsync("Senf", true);

    ErrorOr<SavedItemView> saved = await _service.SetRecipeLineAsync(Guid.NewGuid(), mustard.Id, AmountRequest(1), TestContext.CurrentContext.CancellationToken);

    Assert.That(saved.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public async Task SetRecipeLineAsync_AnUnknownIngredient_IsRefusedAsNotFound()
  {
    ErrorOr<SavedItemView> saved = await _service.SetRecipeLineAsync(_seeded.SausageItemId, Guid.NewGuid(), AmountRequest(1), TestContext.CurrentContext.CancellationToken);

    Assert.That(saved.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public async Task RemoveRecipeLineAsync_ALineExists_RemovesOnlyThatLine()
  {
    var mustard = await AddStoredIngredientAsync("Senf", true);
    var ketchup = await AddStoredIngredientAsync("Gurkenscheibe", true);
    AddRecipeLine(_seeded.SausageItemId, mustard, 10);
    AddRecipeLine(_seeded.SausageItemId, ketchup, 5);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    ErrorOr<Success> removed = await _service.RemoveRecipeLineAsync(_seeded.SausageItemId, mustard.Id, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(removed.IsError, Is.False);
                      Assert.That(_fixture.DbContext.CatalogItemIngredients.Select(recipeLine => recipeLine.IngredientId), Is.EqualTo(new[] { ketchup.Id }));
                    });
  }

  [Test]
  public async Task RemoveRecipeLineAsync_NoSuchLine_IsRefusedAsNotFound()
  {
    ErrorOr<Success> removed = await _service.RemoveRecipeLineAsync(_seeded.SausageItemId, Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(removed.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  private SaveItemRequest RequestFor(string name, Guid? categoryId, double? productionMinutes = null, int sortOrder = 1, bool isQueueIndependent = false)
  {
    return new()
           {
             Name = name,
             CategoryId = categoryId,
             SortOrder = sortOrder,
             ProductionMinutes = productionMinutes,
             IsQueueIndependent = isQueueIndependent
           };
  }

  private SaveItemIngredientRequest AmountRequest(double? amount)
  {
    return new() { Amount = amount };
  }

  private Unit AddUnit(string name)
  {
    Unit unit = new()
                {
                  Id = Guid.NewGuid(),
                  Name = name,
                  IsActive = true
                };
    _fixture.DbContext.Units.Add(unit);

    return unit;
  }

  private Ingredient AddIngredient(string name, Unit unit, bool isActive)
  {
    Ingredient ingredient = new()
                            {
                              Id = Guid.NewGuid(),
                              Name = name,
                              UnitId = unit.Id,
                              Unit = unit,
                              IsActive = isActive
                            };
    _fixture.DbContext.Ingredients.Add(ingredient);

    return ingredient;
  }

  private async Task<Ingredient> AddStoredIngredientAsync(string name, bool isActive)
  {
    var ingredient = AddIngredient(name, _gram, isActive);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return ingredient;
  }

  private void AddRecipeLine(Guid itemId, Ingredient ingredient, double amount)
  {
    _fixture.DbContext.CatalogItemIngredients.Add(new()
                                                  {
                                                    Id = Guid.NewGuid(),
                                                    CatalogItemId = itemId,
                                                    IngredientId = ingredient.Id,
                                                    Amount = amount
                                                  });
  }

  private CatalogItem AddSwitchedOffArticle(Guid categoryId)
  {
    CatalogItem article = new()
                          {
                            Id = Guid.NewGuid(),
                            Name = "Currywurst",
                            CategoryId = categoryId,
                            SortOrder = 9,
                            IsActive = false
                          };
    _fixture.DbContext.CatalogItems.Add(article);

    return article;
  }

  private async Task<CatalogItem> GivenASwitchedOffArticleWithTheRecipeAsync(Guid categoryId, params (string Name, bool IsActive)[] ingredients)
  {
    var currywurst = AddSwitchedOffArticle(categoryId);

    foreach (var ingredient in ingredients)
      AddRecipeLine(currywurst.Id, AddIngredient(ingredient.Name, _gram, ingredient.IsActive), 1);

    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    return currywurst;
  }
}
