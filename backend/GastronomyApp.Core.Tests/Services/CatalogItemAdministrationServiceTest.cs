using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;

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
    _clock = A.Fake<IClock>();
    _transactionRunner = new();

    A.CallTo(() => _clock.UtcNow).Returns(_now);
    A.CallTo(() => _festivalRepository.ExistsAsync(A<Guid>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._))
     .Returns(Task.FromResult<Festival?>(null));
    A.CallTo(() => _itemRepository.IsNameTakenAsync(A<string>._, A<Guid?>._, A<CancellationToken>._)).Returns(false);
    A.CallTo(() => _itemRepository.FindByIdAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<CatalogItem?>(null));
    A.CallTo(() => _itemRepository.FindMenuRowAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<FestivalCatalogItem?>(null));
    A.CallTo(() => _categoryRepository.FindByIdAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<CatalogCategory?>(null));
    A.CallTo(() => _itemRepository.FindByIdAsync(_bratwurstId, A<CancellationToken>._))
     .Returns(Task.FromResult<CatalogItem?>(BuildItem(_bratwurstId, "Bratwurst", _foodCategoryId, true)));
    A.CallTo(() => _categoryRepository.FindByIdAsync(_foodCategoryId, A<CancellationToken>._))
     .Returns(Task.FromResult<CatalogCategory?>(BuildCategory(_foodCategoryId, true)));
    A.CallTo(() => _categoryRepository.FindByIdAsync(_switchedOffCategoryId, A<CancellationToken>._))
     .Returns(Task.FromResult<CatalogCategory?>(BuildCategory(_switchedOffCategoryId, false)));

    _service = new(_itemRepository, _categoryRepository, _festivalRepository, _transactionRunner, _clock);
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _bratwurstId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _foodCategoryId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _kitchenId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
  private readonly Guid _switchedOffCategoryId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

  private ICatalogCategoryRepository _categoryRepository = null!;
  private IClock _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private ICatalogItemRepository _itemRepository = null!;
  private CatalogItemAdministrationService _service = null!;
  private RecordingTransactionRunner _transactionRunner = null!;

  [Test]
  public void CreateAsync_NullRequest_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.CreateAsync(null!, CancellationToken.None),
                Throws.ArgumentNullException);
  }

  [Test]
  public void UpdateAsync_NullRequest_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _service.UpdateAsync(_bratwurstId, null!, CancellationToken.None),
                Throws.ArgumentNullException);
  }

  [Test]
  public async Task ListAsync_FestivalThatIsNotThere_FailsBecauseTheFestivalIsNotFound()
  {
    A.CallTo(() => _festivalRepository.ExistsAsync(_festivalId, A<CancellationToken>._)).Returns(false);

    Result<IReadOnlyList<AdministeredCatalogItem>, CatalogItemAdministrationFailure> listed =
      await _service.ListAsync(_festivalId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.IsSuccess, Is.False);
                      Assert.That(listed.Failure.Reason,
                                  Is.EqualTo(CatalogItemAdministrationFailureReason.FestivalNotFound));
                    });
  }

  [Test]
  public async Task ListAsync_AFestivalTheItemIsOnTheMenuOf_CarriesThePriceAndTheStations()
  {
    A.CallTo(() => _itemRepository.FindAllOrderedAsync(A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<CatalogItem>>([BuildItem(_bratwurstId, "Bratwurst", _foodCategoryId, true)]));
    A.CallTo(() => _itemRepository.FindMenuRowsAtFestivalAsync(_festivalId, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<FestivalCatalogItem>>([
                                                                    new()
                                                                    {
                                                                      Id = Guid.NewGuid(),
                                                                      FestivalId = _festivalId,
                                                                      CatalogItemId = _bratwurstId,
                                                                      PriceCents = 350,
                                                                      IsAvailable = true
                                                                    }
                                                                  ]));
    A.CallTo(() => _itemRepository.FindAssignmentsAtFestivalAsync(_festivalId, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<ItemStationAssignment>>([
                                                                      new()
                                                                      {
                                                                        Id = Guid.NewGuid(),
                                                                        FestivalId = _festivalId,
                                                                        CatalogItemId = _bratwurstId,
                                                                        StationId = _kitchenId
                                                                      }
                                                                    ]));

    Result<IReadOnlyList<AdministeredCatalogItem>, CatalogItemAdministrationFailure> listed =
      await _service.ListAsync(_festivalId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.IsSuccess, Is.True);
                      Assert.That(listed.Value.Single().AtTheFestival!.PriceCents, Is.EqualTo(350));
                      Assert.That(listed.Value.Single().AtTheFestival!.StationIds, Is.EqualTo(new[] { _kitchenId }));
                    });
  }

  [Test]
  public async Task CreateAsync_NameOfAnotherItem_FailsBecauseTheNameIsTaken()
  {
    A.CallTo(() => _itemRepository.IsNameTakenAsync("Bratwurst", null, A<CancellationToken>._)).Returns(true);

    Result<Guid, CatalogItemAdministrationFailure> created =
      await _service.CreateAsync(RequestFor("Bratwurst", _foodCategoryId), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.Failure.Reason, Is.EqualTo(CatalogItemAdministrationFailureReason.NameTaken));
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });
  }

  [Test]
  public async Task CreateAsync_NameOfOnlySpaces_FailsBecauseTheNameIsMissing()
  {
    Result<Guid, CatalogItemAdministrationFailure> created =
      await _service.CreateAsync(RequestFor("   ", _foodCategoryId), CancellationToken.None);

    Assert.That(created.Failure.Reason, Is.EqualTo(CatalogItemAdministrationFailureReason.NameMissing));
  }

  [Test]
  public async Task CreateAsync_CategoryThatIsNotThere_FailsBecauseTheCategoryIsUnknown()
  {
    Result<Guid, CatalogItemAdministrationFailure> created =
      await _service.CreateAsync(RequestFor("Currywurst", Guid.NewGuid()), CancellationToken.None);

    Assert.That(created.Failure.Reason, Is.EqualTo(CatalogItemAdministrationFailureReason.CategoryUnknown));
  }

  [Test]
  public async Task CreateAsync_CategoryThatIsSwitchedOff_FailsBecauseTheCategoryIsSwitchedOff()
  {
    Result<Guid, CatalogItemAdministrationFailure> created =
      await _service.CreateAsync(RequestFor("Currywurst", _switchedOffCategoryId), CancellationToken.None);

    Assert.That(created.Failure.Reason, Is.EqualTo(CatalogItemAdministrationFailureReason.CategoryIsSwitchedOff));
  }

  [TestCase(-1d)]
  [TestCase(601d)]
  [TestCase(1.25d)]
  public async Task CreateAsync_PreparationTimeTheItemFormNeverProduces_FailsBecauseItIsOutOfRange(double minutes)
  {
    Result<Guid, CatalogItemAdministrationFailure> created =
      await _service.CreateAsync(RequestFor("Currywurst", _foodCategoryId) with { ProductionMinutes = minutes },
                                 CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.Failure.Reason,
                                  Is.EqualTo(CatalogItemAdministrationFailureReason.ProductionMinutesOutOfRange));
                      Assert.That(created.Failure.OffendingProductionMinutes, Is.EqualTo(minutes));
                    });
  }

  [Test]
  public async Task CreateAsync_AnItemNothingRefuses_StoresItAndCommits()
  {
    Result<Guid, CatalogItemAdministrationFailure> created =
      await _service.CreateAsync(RequestFor("Currywurst", _foodCategoryId) with { ProductionMinutes = 1.5 },
                                 CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsSuccess, Is.True);
                      Assert.That(_transactionRunner.Committed, Is.True);
                    });

    A.CallTo(() => _itemRepository.AddAsync(A<CatalogItem>.That.Matches(item => item.Name == "Currywurst"
                                                                                && item.IsActive
                                                                                && item.ProductionMinutes == 1.5),
                                            A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _itemRepository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task UpdateAsync_ItemThatIsNotThere_FailsBecauseTheItemIsNotFound()
  {
    Result<Guid, CatalogItemAdministrationFailure> updated =
      await _service.UpdateAsync(Guid.NewGuid(), RequestFor("Currywurst", _foodCategoryId), CancellationToken.None);

    Assert.That(updated.Failure.Reason, Is.EqualTo(CatalogItemAdministrationFailureReason.ItemNotFound));
  }

  [Test]
  public async Task ActivateAsync_ItemWhoseCategoryIsSwitchedOff_FailsBecauseTheCategoryIsSwitchedOff()
  {
    var itemId = Guid.NewGuid();
    A.CallTo(() => _itemRepository.FindByIdAsync(itemId, A<CancellationToken>._))
     .Returns(Task.FromResult<CatalogItem?>(BuildItem(itemId, "Pommes", _switchedOffCategoryId, false)));

    Result<Guid, CatalogItemAdministrationFailure> switchedOn =
      await _service.ActivateAsync(itemId, CancellationToken.None);

    Assert.That(switchedOn.Failure.Reason,
                Is.EqualTo(CatalogItemAdministrationFailureReason.CategoryIsSwitchedOff));
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

    Result<Guid, CatalogItemAdministrationFailure> switchedOff =
      await _service.DeactivateAsync(_bratwurstId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.Failure.Reason,
                                  Is.EqualTo(CatalogItemAdministrationFailureReason.ItemIsOnTheRunningFestivalsMenu));
                      Assert.That(_transactionRunner.Committed, Is.False);
                    });
  }

  [Test]
  public async Task DeactivateAsync_NoFestivalIsRunning_SwitchesTheItemOff()
  {
    Result<Guid, CatalogItemAdministrationFailure> switchedOff =
      await _service.DeactivateAsync(_bratwurstId, CancellationToken.None);

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

  private SaveCatalogItemRequest RequestFor(string name, Guid categoryId)
  {
    return new()
           {
             Name = name,
             CategoryId = categoryId,
             SortOrder = 1
           };
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
