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
public sealed class TableAdministrationServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<ITablePlanRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _clock = new FakeTimeProvider(new(_now));

    A.CallTo(() => _festivalRepository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival(false)));
    A.CallTo(() => _repository.FindAreaAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Area?>(null));
    A.CallTo(() => _repository.FindAreaAsync(_festivalId, _gardenId, A<CancellationToken>._)).Returns(Task.FromResult<Area?>(BuildArea(_gardenId, "Garten")));
    A.CallTo(() => _repository.FindAreaAsync(_festivalId, _hallId, A<CancellationToken>._)).Returns(Task.FromResult<Area?>(BuildArea(_hallId, "Halle")));
    A.CallTo(() => _repository.FindTableAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Table?>(null));
    A.CallTo(() => _repository.IsTableNameTakenAsync(A<Guid>._, A<string>._, A<Guid?>._, A<CancellationToken>._)).Returns(false);

    _service = new(_repository, _festivalRepository, new(_festivalRepository, new(), _clock), new());
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _gardenId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
  private readonly Guid _hallId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
  private readonly Guid _tableId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

  private TimeProvider _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private ITablePlanRepository _repository = null!;
  private TableAdministrationService _service = null!;

  [Test]
  public async Task CreateAsync_AnAreaOfAnotherFestival_IsRefusedGenericallyAndAddsNothing()
  {
    var strangerAreaId = Guid.NewGuid();

    ErrorOr<Table> created = await _service.CreateAsync(_festivalId, BuildRequest(strangerAreaId, "Tisch 1"), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(created.RefusalDescription(), Does.Contain(strangerAreaId.ToString()));
                    });

    A.CallTo(() => _repository.AddTableAsync(A<Table>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CreateAsync_ANameAnotherTableOfTheFestivalCarries_IsRefusedAsTaken()
  {
    A.CallTo(() => _repository.IsTableNameTakenAsync(_festivalId, "Tisch 1", null, A<CancellationToken>._)).Returns(true);

    ErrorOr<Table> created = await _service.CreateAsync(_festivalId, BuildRequest(_gardenId, "Tisch 1"), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.tableNameTaken"));
                      Assert.That(created.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("TableNameTaken"));
                    });

    A.CallTo(() => _repository.AddTableAsync(A<Table>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CreateAsync_AGeometryAsKonvaHandsItOver_StoresItExactly()
  {
    ErrorOr<Table> created = await _service.CreateAsync(_festivalId,
                                                        new()
                                                        {
                                                          AreaId = _gardenId,
                                                          Name = "Tisch 1",
                                                          Geometry = new()
                                                                     {
                                                                       X = -12.3456789,
                                                                       Y = 1234.5,
                                                                       Width = 0.1,
                                                                       Height = 98765.4321,
                                                                       Rotation = -179.99
                                                                     }
                                                        },
                                                        CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.Value.FestivalId, Is.EqualTo(_festivalId));
                      Assert.That(created.Value.AreaId, Is.EqualTo(_gardenId));
                      Assert.That(created.Value.Name, Is.EqualTo("Tisch 1"));
                      Assert.That(created.Value.X, Is.EqualTo(-12.3456789));
                      Assert.That(created.Value.Y, Is.EqualTo(1234.5));
                      Assert.That(created.Value.Width, Is.EqualTo(0.1));
                      Assert.That(created.Value.Height, Is.EqualTo(98765.4321));
                      Assert.That(created.Value.Rotation, Is.EqualTo(-179.99));
                      Assert.That(created.Value.IsHidden, Is.False);
                    });

    A.CallTo(() => _repository.AddTableAsync(created.Value, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task UpdateAsync_ATableTheFestivalDoesNotHave_IsNotFound()
  {
    ErrorOr<Table> updated = await _service.UpdateAsync(_festivalId, _tableId, BuildRequest(_gardenId, "Tisch 1"), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("TableNotFound"));
                    });
  }

  [Test]
  public async Task UpdateAsync_AnAreaOfAnotherFestival_IsRefusedGenericallyAndMovesNothing()
  {
    var table = BuildTable();
    var strangerAreaId = Guid.NewGuid();
    A.CallTo(() => _repository.FindTableAsync(_festivalId, _tableId, A<CancellationToken>._)).Returns(Task.FromResult<Table?>(table));

    ErrorOr<Table> updated = await _service.UpdateAsync(_festivalId, _tableId, BuildRequest(strangerAreaId, "Tisch 1"), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(updated.RefusalDescription(), Does.Contain(strangerAreaId.ToString()));
                      Assert.That(table.AreaId, Is.EqualTo(_gardenId));
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task UpdateAsync_AnotherAreaOfTheFestival_MovesTheTableThereWithItsNewGeometry()
  {
    var table = BuildTable();
    A.CallTo(() => _repository.FindTableAsync(_festivalId, _tableId, A<CancellationToken>._)).Returns(Task.FromResult<Table?>(table));

    ErrorOr<Table> updated = await _service.UpdateAsync(_festivalId,
                                                        _tableId,
                                                        new()
                                                        {
                                                          AreaId = _hallId,
                                                          Name = "Stammtisch",
                                                          Geometry = new()
                                                                     {
                                                                       X = 5.5,
                                                                       Y = 6.25,
                                                                       Width = 140,
                                                                       Height = 70.5,
                                                                       Rotation = -45
                                                                     }
                                                        },
                                                        CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.Value.AreaId, Is.EqualTo(_hallId));
                      Assert.That(updated.Value.FestivalId, Is.EqualTo(_festivalId));
                      Assert.That(updated.Value.Name, Is.EqualTo("Stammtisch"));
                      Assert.That(updated.Value.X, Is.EqualTo(5.5));
                      Assert.That(updated.Value.Y, Is.EqualTo(6.25));
                      Assert.That(updated.Value.Width, Is.EqualTo(140));
                      Assert.That(updated.Value.Height, Is.EqualTo(70.5));
                      Assert.That(updated.Value.Rotation, Is.EqualTo(-45));
                    });

    A.CallTo(() => _repository.IsTableNameTakenAsync(_festivalId, "Stammtisch", _tableId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task UpdateAsync_ANameAnotherTableCarries_IsRefusedAsTaken()
  {
    var table = BuildTable();
    A.CallTo(() => _repository.FindTableAsync(_festivalId, _tableId, A<CancellationToken>._)).Returns(Task.FromResult<Table?>(table));
    A.CallTo(() => _repository.IsTableNameTakenAsync(_festivalId, "Tisch 2", _tableId, A<CancellationToken>._)).Returns(true);

    ErrorOr<Table> updated = await _service.UpdateAsync(_festivalId, _tableId, BuildRequest(_gardenId, "Tisch 2"), CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.tableNameTaken"));
                      Assert.That(table.Name, Is.EqualTo("Tisch 1"));
                    });
  }

  [Test]
  public async Task HideAsync_AShownTableWhileTheFestivalRuns_HidesIt()
  {
    var table = BuildTable();
    A.CallTo(() => _repository.FindTableAsync(_festivalId, _tableId, A<CancellationToken>._)).Returns(Task.FromResult<Table?>(table));

    ErrorOr<Table> hidden = await _service.HideAsync(_festivalId, _tableId, CancellationToken.None);

    Assert.That(hidden.Value.IsHidden, Is.True);

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task HideAsync_ATableWithItemsStillToHandOutOrSettleWhileTheFestivalRuns_IsRefusedAndStaysShown()
  {
    var table = BuildTable();
    A.CallTo(() => _repository.FindTableAsync(_festivalId, _tableId, A<CancellationToken>._)).Returns(Task.FromResult<Table?>(table));
    A.CallTo(() => _repository.HasItemsToHandOutOrSettleAtTableAsync(_tableId, A<CancellationToken>._)).Returns(true);

    ErrorOr<Table> hidden = await _service.HideAsync(_festivalId, _tableId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(hidden.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.tableWithItemsToHandOutOrSettleStaysShown"));
                      Assert.That(hidden.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("TableWithItemsToHandOutOrSettleStaysShown"));
                      Assert.That(table.IsHidden, Is.False);
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task HideAsync_ATableWithItemsStillToHandOutOrSettleAfterTheFestival_HidesIt()
  {
    A.CallTo(() => _festivalRepository.FindByIdAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival(true)));
    var table = BuildTable();
    A.CallTo(() => _repository.FindTableAsync(_festivalId, _tableId, A<CancellationToken>._)).Returns(Task.FromResult<Table?>(table));
    A.CallTo(() => _repository.HasItemsToHandOutOrSettleAtTableAsync(_tableId, A<CancellationToken>._)).Returns(true);

    ErrorOr<Table> hidden = await _service.HideAsync(_festivalId, _tableId, CancellationToken.None);

    Assert.That(hidden.Value.IsHidden, Is.True);

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task HideAsync_ATableAlreadyHiddenWithItemsToHandOutOrSettleWhileTheFestivalRuns_AnswersTheHiddenTable()
  {
    var table = BuildTable();
    table.IsHidden = true;
    A.CallTo(() => _repository.FindTableAsync(_festivalId, _tableId, A<CancellationToken>._)).Returns(Task.FromResult<Table?>(table));
    A.CallTo(() => _repository.HasItemsToHandOutOrSettleAtTableAsync(_tableId, A<CancellationToken>._)).Returns(true);

    ErrorOr<Table> hidden = await _service.HideAsync(_festivalId, _tableId, CancellationToken.None);

    Assert.That(hidden.Value.IsHidden, Is.True);

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task ShowAsync_AHiddenTable_ShowsItAgain()
  {
    var table = BuildTable();
    table.IsHidden = true;
    A.CallTo(() => _repository.FindTableAsync(_festivalId, _tableId, A<CancellationToken>._)).Returns(Task.FromResult<Table?>(table));

    ErrorOr<Table> shown = await _service.ShowAsync(_festivalId, _tableId, CancellationToken.None);

    Assert.That(shown.Value.IsHidden, Is.False);
  }

  [Test]
  public async Task HideAsync_ATableTheFestivalDoesNotHave_IsNotFound()
  {
    ErrorOr<Table> hidden = await _service.HideAsync(_festivalId, _tableId, CancellationToken.None);

    Assert.That(hidden.RefusalMessageKey(), Is.EqualTo("TableNotFound"));
  }

  [Test]
  public async Task DeleteAsync_WhileTheFestivalRuns_IsRefusedAndRemovesNothing()
  {
    A.CallTo(() => _repository.FindTableAsync(_festivalId, _tableId, A<CancellationToken>._)).Returns(Task.FromResult<Table?>(BuildTable()));

    ErrorOr<Table> deleted = await _service.DeleteAsync(_festivalId, _tableId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(deleted.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(deleted.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.tableStaysWhileTheFestivalRuns"));
                      Assert.That(deleted.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("TableStaysWhileTheFestivalRuns"));
                    });

    A.CallTo(() => _repository.RemoveTable(A<Table>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task DeleteAsync_AfterTheFestival_RemovesTheTable()
  {
    A.CallTo(() => _festivalRepository.FindByIdAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival(true)));
    var table = BuildTable();
    A.CallTo(() => _repository.FindTableAsync(_festivalId, _tableId, A<CancellationToken>._)).Returns(Task.FromResult<Table?>(table));

    ErrorOr<Table> deleted = await _service.DeleteAsync(_festivalId, _tableId, CancellationToken.None);

    Assert.That(deleted.IsError, Is.False);

    A.CallTo(() => _repository.RemoveTable(table)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task DeleteAsync_ATableWithOrdersAfterTheFestival_IsRefusedAndRemovesNothing()
  {
    A.CallTo(() => _festivalRepository.FindByIdAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(BuildFestival(true)));
    A.CallTo(() => _repository.FindTableAsync(_festivalId, _tableId, A<CancellationToken>._)).Returns(Task.FromResult<Table?>(BuildTable()));
    A.CallTo(() => _repository.HasOrdersAtTableAsync(_tableId, A<CancellationToken>._)).Returns(true);

    ErrorOr<Table> deleted = await _service.DeleteAsync(_festivalId, _tableId, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(deleted.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(deleted.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.tableWithOrdersStays"));
                      Assert.That(deleted.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("TableWithOrdersStays"));
                    });

    A.CallTo(() => _repository.RemoveTable(A<Table>._)).MustNotHaveHappened();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  private SaveTableRequest BuildRequest(Guid areaId, string name)
  {
    return new()
           {
             AreaId = areaId,
             Name = name,
             Geometry = new()
                        {
                          X = 10,
                          Y = 20,
                          Width = 80,
                          Height = 40,
                          Rotation = 0
                        }
           };
  }

  private Area BuildArea(Guid areaId, string name)
  {
    return new()
           {
             Id = areaId,
             FestivalId = _festivalId,
             Name = name,
             SortOrder = 1,
             IsHidden = false
           };
  }

  private Table BuildTable()
  {
    return new()
           {
             Id = _tableId,
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
