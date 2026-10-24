using ErrorOr;
using FakeItEasy;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
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
    A.CallTo(() => _repository.FindAreaWithContentsAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Area?>(null));
    A.CallTo(() => _repository.IsAreaNameTakenAsync(A<Guid>._, A<string>._, A<Guid?>._, A<CancellationToken>._)).Returns(false);

    _service = new(_repository, _festivalRepository, new(_festivalRepository, new(), _clock));
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

    ErrorOr<IReadOnlyList<Area>> tablePlan = await _service.ListTablePlanAsync(_festivalId, CancellationToken.None);

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
    var garden = BuildGarden(false);
    A.CallTo(() => _repository.FindTablePlanAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Area>>([garden]));

    ErrorOr<IReadOnlyList<Area>> tablePlan = await _service.ListTablePlanAsync(_festivalId, CancellationToken.None);

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
  public async Task DeleteAsync_WhileTheFestivalRuns_IsRefusedAndRemovesNothing()
  {
    A.CallTo(() => _repository.FindAreaWithContentsAsync(_festivalId, _gardenId, A<CancellationToken>._)).Returns(Task.FromResult<Area?>(BuildGarden(false)));

    ErrorOr<Area> deleted = await _service.DeleteAsync(_festivalId, _gardenId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(deleted.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(deleted.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.areaStaysWhileTheFestivalRuns"));
                      Assert.That(deleted.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("AreaStaysWhileTheFestivalRuns"));
                    });

    A.CallTo(() => _repository.RemoveArea(A<Area>._)).MustNotHaveHappened();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task DeleteAsync_AfterTheFestival_RemovesTheAreaWithItsTablesAndStationShapes()
  {
    A.CallTo(() => _festivalRepository.FindByIdAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival(true)));
    var garden = BuildGarden(false);
    var table = BuildTable();
    var shape = BuildShape();
    garden.Tables.Add(table);
    garden.StationShapes.Add(shape);
    A.CallTo(() => _repository.FindAreaWithContentsAsync(_festivalId, _gardenId, A<CancellationToken>._)).Returns(Task.FromResult<Area?>(garden));

    ErrorOr<Area> deleted = await _service.DeleteAsync(_festivalId, _gardenId, CancellationToken.None);

    Assert.That(deleted.IsError, Is.False);

    A.CallTo(() => _repository.RemoveTables(A<IReadOnlyCollection<Table>>.That.IsSameSequenceAs(new[] { table }))).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repository.RemoveStationShapes(A<IReadOnlyCollection<FestivalStationShape>>.That.IsSameSequenceAs(new[] { shape }))).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repository.RemoveArea(garden)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task DeleteAsync_AnAreaTheFestivalDoesNotHave_IsNotFound()
  {
    ErrorOr<Area> deleted = await _service.DeleteAsync(_festivalId, _gardenId, CancellationToken.None);

    Assert.That(deleted.RefusalMessageKey(), Is.EqualTo("AreaNotFound"));
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

  private Table BuildTable()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             FestivalId = _festivalId,
             AreaId = _gardenId,
             Name = "Tisch 1",
             X = 10,
             Y = 20,
             Width = 80,
             Height = 40,
             Rotation = 0,
             IsHidden = false
           };
  }

  private FestivalStationShape BuildShape()
  {
    return new()
           {
             FestivalStationId = Guid.NewGuid(),
             FestivalId = _festivalId,
             AreaId = _gardenId,
             X = 200,
             Y = 30,
             Width = 60,
             Height = 60,
             Rotation = 0
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
