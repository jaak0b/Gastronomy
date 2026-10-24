using ErrorOr;
using FakeItEasy;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class AreaAdministrationServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<ITablePlanRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _clock = new FakeTimeProvider(new(_now));

    A.CallTo(() => _festivalRepository.ExistsAsync(A<Guid>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _festivalRepository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival(false)));
    A.CallTo(() => _repository.FindAreaAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Area?>(null));
    A.CallTo(() => _repository.IsAreaNameTakenAsync(A<Guid>._, A<string>._, A<Guid?>._, A<CancellationToken>._)).Returns(false);

    _service = new(_repository, _festivalRepository, new(_festivalRepository, new(), _clock), new());
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _gardenId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

  private TimeProvider _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private ITablePlanRepository _repository = null!;
  private AreaAdministrationService _service = null!;

  [Test]
  public async Task ListTablePlanAsync_AFestivalThatIsNotThere_IsNotFound()
  {
    A.CallTo(() => _festivalRepository.ExistsAsync(_festivalId, A<CancellationToken>._)).Returns(false);

    ErrorOr<IReadOnlyList<ListedArea>> tablePlan = await _service.ListTablePlanAsync(_festivalId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(tablePlan.IsError, Is.True);
                      Assert.That(tablePlan.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(tablePlan.RefusalMessageKey(), Is.EqualTo("FestivalNotFound"));
                    });
  }

  [Test]
  public async Task ListTablePlanAsync_AFestivalWithAreas_HandsOverWhatTheRepositoryFound()
  {
    ListedArea garden = new(BuildGarden(false), true, [], []);
    A.CallTo(() => _repository.FindTablePlanAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<ListedArea>>([garden]));

    ErrorOr<IReadOnlyList<ListedArea>> tablePlan = await _service.ListTablePlanAsync(_festivalId, CancellationToken.None);

    Assert.That(tablePlan.Value, Is.EqualTo(new[] { garden }));
  }

  [Test]
  public async Task CreateAsync_ANameAnotherAreaOfTheFestivalCarries_IsRefusedAsTaken()
  {
    A.CallTo(() => _repository.IsAreaNameTakenAsync(_festivalId, "Garten", null, A<CancellationToken>._)).Returns(true);

    ErrorOr<Area> created = await _service.CreateAsync(_festivalId, BuildRequest("Garten", 1), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.areaNameTaken"));
                      Assert.That(created.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("AreaNameTaken"));
                    });

    A.CallTo(() => _repository.AddAreaAsync(A<Area>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CreateAsync_AFestivalThatIsNotThere_IsNotFound()
  {
    A.CallTo(() => _festivalRepository.ExistsAsync(_festivalId, A<CancellationToken>._)).Returns(false);

    ErrorOr<Area> created = await _service.CreateAsync(_festivalId, BuildRequest("Garten", 1), CancellationToken.None);

    Assert.That(created.RefusalMessageKey(), Is.EqualTo("FestivalNotFound"));
  }

  [Test]
  public async Task CreateAsync_AFreeName_AddsAShownAreaWithTheNameAndSortOrderAsGiven()
  {
    ErrorOr<Area> created = await _service.CreateAsync(_festivalId, BuildRequest("Zelt am See", 4), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.Value.FestivalId, Is.EqualTo(_festivalId));
                      Assert.That(created.Value.Name, Is.EqualTo("Zelt am See"));
                      Assert.That(created.Value.SortOrder, Is.EqualTo(4));
                      Assert.That(created.Value.IsHidden, Is.False);
                    });

    A.CallTo(() => _repository.AddAreaAsync(created.Value, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task UpdateAsync_AnAreaTheFestivalDoesNotHave_IsNotFound()
  {
    ErrorOr<Area> updated = await _service.UpdateAsync(_festivalId, _gardenId, BuildRequest("Garten", 1), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("AreaNotFound"));
                      Assert.That(updated.RefusalDescription(), Does.Contain(_gardenId.ToString()));
                    });
  }

  [Test]
  public async Task UpdateAsync_ANewNameAndSortOrder_LooksForNamesakesOtherThanTheAreaItself()
  {
    var garden = BuildGarden(false);
    A.CallTo(() => _repository.FindAreaAsync(_festivalId, _gardenId, A<CancellationToken>._)).Returns(Task.FromResult<Area?>(garden));

    ErrorOr<Area> updated = await _service.UpdateAsync(_festivalId, _gardenId, BuildRequest("garten", 7), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.Value.Name, Is.EqualTo("garten"));
                      Assert.That(updated.Value.SortOrder, Is.EqualTo(7));
                    });

    A.CallTo(() => _repository.IsAreaNameTakenAsync(_festivalId, "garten", _gardenId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task UpdateAsync_ANameAnotherAreaCarries_KeepsTheOldName()
  {
    var garden = BuildGarden(false);
    A.CallTo(() => _repository.FindAreaAsync(_festivalId, _gardenId, A<CancellationToken>._)).Returns(Task.FromResult<Area?>(garden));
    A.CallTo(() => _repository.IsAreaNameTakenAsync(_festivalId, "Halle", _gardenId, A<CancellationToken>._)).Returns(true);

    ErrorOr<Area> updated = await _service.UpdateAsync(_festivalId, _gardenId, BuildRequest("Halle", 1), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.areaNameTaken"));
                      Assert.That(garden.Name, Is.EqualTo("Garten"));
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task HideAsync_AShownAreaWhileTheFestivalRuns_HidesIt()
  {
    var garden = BuildGarden(false);
    A.CallTo(() => _repository.FindAreaAsync(_festivalId, _gardenId, A<CancellationToken>._)).Returns(Task.FromResult<Area?>(garden));

    ErrorOr<Area> hidden = await _service.HideAsync(_festivalId, _gardenId, CancellationToken.None);

    Assert.That(hidden.Value.IsHidden, Is.True);

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task HideAsync_AnAreaWithItemsStillToHandOutOrSettleWhileTheFestivalRuns_IsRefusedAndStaysShown()
  {
    var garden = BuildGarden(false);
    A.CallTo(() => _repository.FindAreaAsync(_festivalId, _gardenId, A<CancellationToken>._)).Returns(Task.FromResult<Area?>(garden));
    A.CallTo(() => _repository.HasItemsToHandOutOrSettleInAreaAsync(_gardenId, A<CancellationToken>._)).Returns(true);

    ErrorOr<Area> hidden = await _service.HideAsync(_festivalId, _gardenId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(hidden.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.areaWithItemsToHandOutOrSettleStaysShown"));
                      Assert.That(hidden.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("AreaWithItemsToHandOutOrSettleStaysShown"));
                      Assert.That(garden.IsHidden, Is.False);
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task HideAsync_AnAreaWithItemsStillToHandOutOrSettleAfterTheFestival_HidesIt()
  {
    A.CallTo(() => _festivalRepository.FindByIdAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival(true)));
    var garden = BuildGarden(false);
    A.CallTo(() => _repository.FindAreaAsync(_festivalId, _gardenId, A<CancellationToken>._)).Returns(Task.FromResult<Area?>(garden));
    A.CallTo(() => _repository.HasItemsToHandOutOrSettleInAreaAsync(_gardenId, A<CancellationToken>._)).Returns(true);

    ErrorOr<Area> hidden = await _service.HideAsync(_festivalId, _gardenId, CancellationToken.None);

    Assert.That(hidden.Value.IsHidden, Is.True);

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task ShowAsync_AHiddenArea_ShowsItAgain()
  {
    var garden = BuildGarden(true);
    A.CallTo(() => _repository.FindAreaAsync(_festivalId, _gardenId, A<CancellationToken>._)).Returns(Task.FromResult<Area?>(garden));

    ErrorOr<Area> shown = await _service.ShowAsync(_festivalId, _gardenId, CancellationToken.None);

    Assert.That(shown.Value.IsHidden, Is.False);
  }

  [Test]
  public async Task ShowAsync_AnAreaTheFestivalDoesNotHave_IsNotFound()
  {
    ErrorOr<Area> shown = await _service.ShowAsync(_festivalId, _gardenId, CancellationToken.None);

    Assert.That(shown.RefusalMessageKey(), Is.EqualTo("AreaNotFound"));
  }

  [Test]
  public async Task MoveAsync_AnAreaUp_SwapsItWithTheOneAboveAndNumbersThemFromOne()
  {
    var garden = BuildArea(_gardenId, "Garten", 1);
    var hall = BuildArea(Guid.NewGuid(), "Halle", 2);
    var tent = BuildArea(Guid.NewGuid(), "Zelt", 3);
    A.CallTo(() => _repository.FindAreasInOrderAsync(_festivalId, A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyList<Area>>([
                                                   garden,
                                                   hall,
                                                   tent
                                                 ]));

    ErrorOr<Area> moved = await _service.MoveAsync(_festivalId, tent.Id, MoveDirection.Up, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.Value, Is.SameAs(tent));
                      Assert.That(new[]
                                  {
                                    garden.SortOrder,
                                    hall.SortOrder,
                                    tent.SortOrder
                                  },
                                  Is.EqualTo(new[]
                                             {
                                               1,
                                               3,
                                               2
                                             }));
                    });
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task MoveAsync_TheFirstAreaUp_ChangesNothing()
  {
    var garden = BuildArea(_gardenId, "Garten", 1);
    var hall = BuildArea(Guid.NewGuid(), "Halle", 2);
    A.CallTo(() => _repository.FindAreasInOrderAsync(_festivalId, A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyList<Area>>([
                                                   garden,
                                                   hall
                                                 ]));

    ErrorOr<Area> moved = await _service.MoveAsync(_festivalId, _gardenId, MoveDirection.Up, CancellationToken.None);

    Assert.That(new[]
                {
                  moved.Value.SortOrder,
                  hall.SortOrder
                },
                Is.EqualTo(new[]
                           {
                             1,
                             2
                           }));
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task MoveAsync_AnAreaTheFestivalDoesNotHave_IsNotFound()
  {
    A.CallTo(() => _repository.FindAreasInOrderAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Area>>([]));

    ErrorOr<Area> moved = await _service.MoveAsync(_festivalId, _gardenId, MoveDirection.Down, CancellationToken.None);

    Assert.That(moved.RefusalMessageKey(), Is.EqualTo("AreaNotFound"));
  }

  private Area BuildArea(Guid areaId, string name, int sortOrder)
  {
    return new()
           {
             Id = areaId,
             FestivalId = _festivalId,
             Name = name,
             SortOrder = sortOrder,
             IsHidden = false
           };
  }

  private SaveAreaRequest BuildRequest(string name, int sortOrder)
  {
    return new()
           {
             Name = name,
             SortOrder = sortOrder
           };
  }

  private Area BuildGarden(bool isHidden)
  {
    return new()
           {
             Id = _gardenId,
             FestivalId = _festivalId,
             Name = "Garten",
             SortOrder = 1,
             IsHidden = isHidden
           };
  }

  private Festival BuildFestival(bool hasEnded)
  {
    return new()
           {
             Id = _festivalId,
             Name = "Sommerfest",
             StartsAtUtc = _now.AddHours(-5),
             EndsAtUtc = EndOfTheFestival(hasEnded),
             NextOrderNumber = 1,
             IsHidden = false
           };
  }

  private DateTime EndOfTheFestival(bool hasEnded)
  {
    if (hasEnded)
      return _now.AddHours(-1);

    return _now.AddHours(5);
  }
}
