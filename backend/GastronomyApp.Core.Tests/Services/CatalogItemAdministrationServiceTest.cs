using ErrorOr;
using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class CatalogItemAdministrationServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _itemRepository = A.Fake<ICatalogItemRepository>();
    _categoryRepository = A.Fake<ICatalogCategoryRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _clock = new FakeTimeProvider(new(_now));
    _transactionRunner = new();

    A.CallTo(() => _festivalRepository.ExistsAsync(A<Guid>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));
    A.CallTo(() => _itemRepository.IsNameTakenAsync(A<string>._, A<Guid?>._, A<CancellationToken>._)).Returns(false);
    A.CallTo(() => _itemRepository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<CatalogItem?>(null));
    A.CallTo(() => _itemRepository.FindMenuRowAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<FestivalCatalogItem?>(null));
    A.CallTo(() => _categoryRepository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<CatalogCategory?>(null));
    A.CallTo(() => _itemRepository.FindByIdAsync(_bratwurstId, A<CancellationToken>._)).Returns(Task.FromResult<CatalogItem?>(BuildItem(_bratwurstId, "Bratwurst", _foodCategoryId, true)));
    A.CallTo(() => _categoryRepository.FindByIdAsync(_foodCategoryId, A<CancellationToken>._)).Returns(Task.FromResult<CatalogCategory?>(BuildCategory(_foodCategoryId, true)));
    A.CallTo(() => _categoryRepository.FindByIdAsync(_switchedOffCategoryId, A<CancellationToken>._)).Returns(Task.FromResult<CatalogCategory?>(BuildCategory(_switchedOffCategoryId, false)));

    _service = new(_itemRepository, _categoryRepository, _festivalRepository, new(_festivalRepository, new(), _clock), _transactionRunner);
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _bratwurstId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _foodCategoryId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _kitchenId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
  private readonly Guid _switchedOffCategoryId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

  private ICatalogCategoryRepository _categoryRepository = null!;
  private TimeProvider _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private ICatalogItemRepository _itemRepository = null!;
  private CatalogItemAdministrationService _service = null!;
  private RecordingTransactionRunner _transactionRunner = null!;

  [Test]
  public async Task ListAsync_FestivalThatIsNotThere_FailsBecauseTheFestivalIsNotFound()
  {
    A.CallTo(() => _festivalRepository.ExistsAsync(_festivalId, A<CancellationToken>._)).Returns(false);

    ErrorOr<IReadOnlyList<CatalogItem>> listed = await _service.ListAsync(_festivalId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.IsSuccess, Is.False);
                      Assert.That(listed.RefusalMessageKey(), Is.EqualTo("FestivalNotFound"));
                    });
  }

  [Test]
  public async Task ListAsync_AFestival_AsksForTheItemsWithWhatTheyCostThere()
  {
    var bratwurst = BuildItem(_bratwurstId, "Bratwurst", _foodCategoryId, true);
    bratwurst.FestivalCatalogItems.Add(new()
                                       {
                                         Id = Guid.NewGuid(),
                                         FestivalId = _festivalId,
                                         CatalogItemId = _bratwurstId,
                                         PriceCents = 350,
                                         IsAvailable = true
                                       });

    A.CallTo(() => _itemRepository.FindAllOrderedAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<CatalogItem>>([bratwurst]));

    ErrorOr<IReadOnlyList<CatalogItem>> listed = await _service.ListAsync(_festivalId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.IsSuccess, Is.True);
                      Assert.That(listed.Value.Single().FestivalCatalogItems.Single().PriceCents, Is.EqualTo(350));
                    });
  }

  [Test]
  public async Task CreateAsync_NameOfAnotherItem_FailsBecauseTheNameIsTaken()
  {
    A.CallTo(() => _itemRepository.IsNameTakenAsync("Bratwurst", null, A<CancellationToken>._)).Returns(true);

    ErrorOr<CatalogItem> created = await CreatedAsync("Bratwurst", _foodCategoryId);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("admin.itemNameTaken"));
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });
  }

  [Test]
  public async Task CreateAsync_CategoryThatIsNotThere_FailsBecauseTheCategoryIsUnknown()
  {
    ErrorOr<CatalogItem> created = await CreatedAsync("Currywurst", Guid.NewGuid());

    Assert.That(created.RefusalMessageKey(), Is.EqualTo("admin.itemCategoryUnknown"));
  }

  [Test]
  public async Task CreateAsync_CategoryThatIsSwitchedOff_FailsBecauseTheCategoryIsSwitchedOff()
  {
    ErrorOr<CatalogItem> created = await CreatedAsync("Currywurst", _switchedOffCategoryId);

    Assert.That(created.RefusalMessageKey(), Is.EqualTo("admin.itemCategoryIsOff"));
  }

  [TestCase(-1d)]
  [TestCase(601d)]
  [TestCase(1.25d)]
  public async Task CreateAsync_PreparationTimeTheItemFormNeverProduces_FailsBecauseItIsOutOfRange(double minutes)
  {
    ErrorOr<CatalogItem> created = await CreatedAsync("Currywurst", _foodCategoryId, productionMinutes: minutes);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("catalog.productionMinutesOutOfRange"));
                      Assert.That(created.RefusalDescription(), Does.Contain(minutes.ToString()));
                    });
  }

  [Test]
  public async Task CreateAsync_AnItemNothingRefuses_StoresItAndCommits()
  {
    ErrorOr<CatalogItem> created = await CreatedAsync("Currywurst", _foodCategoryId, productionMinutes: 1.5);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsSuccess, Is.True);
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });

    A.CallTo(() => _itemRepository.AddAsync(A<CatalogItem>.That.Matches(item => item.Name == "Currywurst" && item.IsActive && item.ProductionMinutes == 1.5), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _itemRepository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task CreateAsync_AnItemNothingRefuses_HandsBackTheCreatedArticle()
  {
    ErrorOr<CatalogItem> created = await CreatedAsync("Currywurst", _foodCategoryId, 4);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsSuccess, Is.True);
                      Assert.That(created.Value.Id, Is.Not.EqualTo(Guid.Empty));
                      Assert.That(created.Value.Name, Is.EqualTo("Currywurst"));
                      Assert.That(created.Value.CategoryId, Is.EqualTo(_foodCategoryId));
                      Assert.That(created.Value.SortOrder, Is.EqualTo(4));
                      Assert.That(created.Value.IsActive, Is.True);
                      Assert.That(created.Value.FestivalCatalogItems, Is.Empty);
                    });
  }

  [Test]
  public async Task UpdateAsync_ItemThatIsNotThere_FailsBecauseTheItemIsNotFound()
  {
    ErrorOr<CatalogItem> updated = await _service.UpdateAsync(Guid.NewGuid(), "Currywurst", _foodCategoryId, 1, null, false, CancellationToken.None);

    Assert.That(updated.RefusalMessageKey(), Is.EqualTo("ItemNotFound"));
  }

  [Test]
  public async Task ActivateAsync_ItemWhoseCategoryIsSwitchedOff_FailsBecauseTheCategoryIsSwitchedOff()
  {
    var itemId = Guid.NewGuid();
    A.CallTo(() => _itemRepository.FindByIdAsync(itemId, A<CancellationToken>._)).Returns(Task.FromResult<CatalogItem?>(BuildItem(itemId, "Pommes", _switchedOffCategoryId, false)));

    ErrorOr<CatalogItem> switchedOn = await _service.ActivateAsync(itemId, CancellationToken.None);

    Assert.That(switchedOn.RefusalMessageKey(), Is.EqualTo("admin.itemCategoryIsOff"));
  }

  [Test]
  public async Task DeactivateAsync_ItemOnTheMenuOfTheRunningFestival_FailsBecauseTheFestivalIsServingIt()
  {
    GivenAFestivalIsRunning();
    A.CallTo(() => _itemRepository.FindMenuRowAsync(_festivalId, _bratwurstId, A<CancellationToken>._))
   .Returns(Task.FromResult<FestivalCatalogItem?>(new()
                                                  {
                                                    Id = Guid.NewGuid(),
                                                    FestivalId = _festivalId,
                                                    CatalogItemId = _bratwurstId,
                                                    PriceCents = 350,
                                                    IsAvailable = true
                                                  }));

    ErrorOr<CatalogItem> switchedOff = await _service.DeactivateAsync(_bratwurstId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.RefusalMessageKey(), Is.EqualTo("admin.itemIsOnTheRunningFestivalsMenu"));
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });
  }

  [Test]
  public async Task DeactivateAsync_NoFestivalIsRunning_SwitchesTheItemOff()
  {
    ErrorOr<CatalogItem> switchedOff = await _service.DeactivateAsync(_bratwurstId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.IsSuccess, Is.True);
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });

    A.CallTo(() => _itemRepository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  private void GivenAFestivalIsRunning()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(_now, A<CancellationToken>._))
   .Returns(Task.FromResult<Festival?>(new()
                                       {
                                         Id = _festivalId,
                                         Name = "Sommerfest",
                                         StartsAtUtc = _now.AddHours(-1),
                                         EndsAtUtc = _now.AddHours(5),
                                         NextOrderNumber = 1,
                                         IsHidden = false
                                       }));
  }

  private Task<ErrorOr<CatalogItem>> CreatedAsync(string? name, Guid? categoryId, int sortOrder = 1, double? productionMinutes = null, bool isQueueIndependent = false)
  {
    return _service.CreateAsync(name, categoryId, sortOrder, productionMinutes, isQueueIndependent, CancellationToken.None);
  }

  private CatalogItem BuildItem(Guid itemId, string name, Guid categoryId, bool isActive)
  {
    return new()
           {
             Id = itemId,
             Name = name,
             CategoryId = categoryId,
             SortOrder = 1,
             IsActive = isActive
           };
  }

  private CatalogCategory BuildCategory(Guid categoryId, bool isActive)
  {
    return new()
           {
             Id = categoryId,
             Name = "Speisen",
             ColourHex = "#C62828",
             SortOrder = 1,
             IsActive = isActive
           };
  }
}
