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
    A.CallTo(() => _repository.FindTablesAsync(A<Guid>._, A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Table>>([]));
    A.CallTo(() => _repository.FindTableNamesAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<string>>([]));
    A.CallTo(() => _repository.IsTableNameTakenAsync(A<Guid>._, A<string>._, A<Guid?>._, A<CancellationToken>._)).Returns(false);

    _service = new(_repository, _festivalRepository, new(A.Fake<IStationRepository>(), _repository, new(_festivalRepository, new(), _clock)), new(), new());
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
  public async Task CopyAsync_TwoTablesOfTheFestival_CreatesBothWithTheNextFreeNamesTheirGeometryAndOneSave()
  {
    var sourceA1 = BuildNamedTable(Guid.Parse("11111111-0000-0000-0000-000000000001"), "A1");
    var sourceA2 = BuildNamedTable(Guid.Parse("11111111-0000-0000-0000-000000000002"), "A2");
    A.CallTo(() => _repository.FindTablesAsync(_festivalId, A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyList<Table>>([
                                                    sourceA1,
                                                    sourceA2
                                                  ]));
    A.CallTo(() => _repository.FindTableNamesAsync(_festivalId, A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyList<string>>([
                                                     "A1",
                                                     "A2"
                                                   ]));

    ErrorOr<IReadOnlyList<Table>> copies = await _service.CopyAsync(_festivalId,
                                                                    new()
                                                                    {
                                                                      AreaId = _hallId,
                                                                      Tables =
                                                                      [
                                                                        BuildCopy(sourceA2.Id, 200.5, 300.25),
                                                                        BuildCopy(sourceA1.Id, 100.5, 200.25)
                                                                      ]
                                                                    },
                                                                    CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(copies.Value.Select(table => table.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "A4",
                                               "A3"
                                             }));
                      Assert.That(copies.Value.Select(table => table.Id), Is.All.Not.EqualTo(sourceA1.Id));
                      Assert.That(copies.Value.Select(table => table.Id), Is.All.Not.EqualTo(sourceA2.Id));
                      Assert.That(copies.Value.Select(table => table.FestivalId), Is.All.EqualTo(_festivalId));
                      Assert.That(copies.Value.Select(table => table.AreaId), Is.All.EqualTo(_hallId));
                      Assert.That(copies.Value.Select(table => table.IsHidden), Is.All.False);
                      Assert.That(copies.Value[0].X, Is.EqualTo(200.5));
                      Assert.That(copies.Value[0].Y, Is.EqualTo(300.25));
                      Assert.That(copies.Value[0].Width, Is.EqualTo(80));
                      Assert.That(copies.Value[0].Height, Is.EqualTo(40));
                      Assert.That(copies.Value[0].Rotation, Is.EqualTo(15.5));
                      Assert.That(copies.Value[1].X, Is.EqualTo(100.5));
                      Assert.That(copies.Value[1].Y, Is.EqualTo(200.25));
                      Assert.That(copies.Value[1].Rotation, Is.EqualTo(15.5));
                    });

    A.CallTo(() => _repository.AddTableAsync(A<Table>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task CopyAsync_ANameAHiddenTableCarries_IsSkippedForTheCopy()
  {
    var source = BuildNamedTable(_tableId, "A1");
    A.CallTo(() => _repository.FindTablesAsync(_festivalId, A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Table>>([source]));
    A.CallTo(() => _repository.FindTableNamesAsync(_festivalId, A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyList<string>>([
                                                     "A1",
                                                     "a2"
                                                   ]));

    ErrorOr<IReadOnlyList<Table>> copies = await _service.CopyAsync(_festivalId,
                                                                    new()
                                                                    {
                                                                      AreaId = _hallId,
                                                                      Tables = [BuildCopy(source.Id, 10, 20)]
                                                                    },
                                                                    CancellationToken.None);

    Assert.That(copies.Value.Single().Name, Is.EqualTo("A3"));
  }

  [Test]
  public async Task CopyAsync_ASourceTheFestivalDoesNotHave_IsNotFoundAndCreatesNothing()
  {
    var source = BuildNamedTable(_tableId, "A1");
    var missingTableId = Guid.NewGuid();
    A.CallTo(() => _repository.FindTablesAsync(_festivalId, A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Table>>([source]));

    ErrorOr<IReadOnlyList<Table>> copies = await _service.CopyAsync(_festivalId,
                                                                    new()
                                                                    {
                                                                      AreaId = _hallId,
                                                                      Tables =
                                                                      [
                                                                        BuildCopy(source.Id, 10, 20),
                                                                        BuildCopy(missingTableId, 10, 60)
                                                                      ]
                                                                    },
                                                                    CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(copies.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(copies.RefusalMessageKey(), Is.EqualTo("TableNotFound"));
                      Assert.That(copies.RefusalDescription(), Does.Contain(missingTableId.ToString()));
                    });

    A.CallTo(() => _repository.AddTableAsync(A<Table>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CopyAsync_ASourceOfAnotherFestival_IsNotFoundAndCreatesNothing()
  {
    var foreignTableId = Guid.NewGuid();
    A.CallTo(() => _repository.FindTablesAsync(_festivalId, A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Table>>([]));

    ErrorOr<IReadOnlyList<Table>> copies = await _service.CopyAsync(_festivalId,
                                                                    new()
                                                                    {
                                                                      AreaId = _hallId,
                                                                      Tables = [BuildCopy(foreignTableId, 10, 20)]
                                                                    },
                                                                    CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(copies.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(copies.RefusalMessageKey(), Is.EqualTo("TableNotFound"));
                      Assert.That(copies.RefusalDescription(), Does.Contain(foreignTableId.ToString()));
                    });

    A.CallTo(() => _repository.AddTableAsync(A<Table>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CopyAsync_ATargetAreaOfAnotherFestival_IsRefusedGenericallyAndCreatesNothing()
  {
    var strangerAreaId = Guid.NewGuid();

    ErrorOr<IReadOnlyList<Table>> copies = await _service.CopyAsync(_festivalId,
                                                                    new()
                                                                    {
                                                                      AreaId = strangerAreaId,
                                                                      Tables = [BuildCopy(_tableId, 10, 20)]
                                                                    },
                                                                    CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(copies.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(copies.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(copies.RefusalDescription(), Does.Contain(strangerAreaId.ToString()));
                    });

    A.CallTo(() => _repository.FindTablesAsync(A<Guid>._, A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _repository.AddTableAsync(A<Table>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CopyAsync_AGeneratedNameLongerThanFortyCharacters_IsRefusedAndCreatesNothing()
  {
    var fortyCharacterName = new string('T', 40);
    var source = BuildNamedTable(_tableId, fortyCharacterName);
    A.CallTo(() => _repository.FindTablesAsync(_festivalId, A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Table>>([source]));
    A.CallTo(() => _repository.FindTableNamesAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<string>>([fortyCharacterName]));

    ErrorOr<IReadOnlyList<Table>> copies = await _service.CopyAsync(_festivalId,
                                                                    new()
                                                                    {
                                                                      AreaId = _hallId,
                                                                      Tables = [BuildCopy(source.Id, 10, 20)]
                                                                    },
                                                                    CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(copies.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(copies.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.tableCopyNameTooLong"));
                      Assert.That(copies.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("TableCopyNameTooLong"));
                      Assert.That(copies.RefusalDescription(), Does.Contain($"{fortyCharacterName} 1"));
                    });

    A.CallTo(() => _repository.AddTableAsync(A<Table>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CopyAsync_NoSourceTable_IsRefusedGenericallyAndCreatesNothing()
  {
    ErrorOr<IReadOnlyList<Table>> copies = await _service.CopyAsync(_festivalId,
                                                                    new()
                                                                    {
                                                                      AreaId = _hallId,
                                                                      Tables = []
                                                                    },
                                                                    CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(copies.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(copies.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(copies.RefusalDescription(), Does.Contain("no source table"));
                    });

    A.CallTo(() => _repository.AddTableAsync(A<Table>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task CopyAsync_TheSameSourceTableTwice_IsRefusedGenericallyAndCreatesNothing()
  {
    ErrorOr<IReadOnlyList<Table>> copies = await _service.CopyAsync(_festivalId,
                                                                    new()
                                                                    {
                                                                      AreaId = _hallId,
                                                                      Tables =
                                                                      [
                                                                        BuildCopy(_tableId, 10, 20),
                                                                        BuildCopy(_tableId, 10, 80)
                                                                      ]
                                                                    },
                                                                    CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(copies.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(copies.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(copies.RefusalDescription(), Does.Contain("more than once"));
                    });

    A.CallTo(() => _repository.FindTablesAsync(A<Guid>._, A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _repository.AddTableAsync(A<Table>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task MoveAsync_TwoTablesOfTheFestival_ReshapesBothAndSavesOnce()
  {
    var sourceA1 = BuildNamedTable(Guid.Parse("11111111-0000-0000-0000-000000000001"), "A1");
    var sourceA2 = BuildNamedTable(Guid.Parse("11111111-0000-0000-0000-000000000002"), "A2");
    A.CallTo(() => _repository.FindTablesAsync(_festivalId, A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
   .Returns(Task.FromResult<IReadOnlyList<Table>>([
                                                    sourceA1,
                                                    sourceA2
                                                  ]));

    ErrorOr<Success> moved = await _service.MoveAsync(_festivalId,
                                                      new()
                                                      {
                                                        Tables =
                                                        [
                                                          BuildMove(sourceA1.Id, 110.5, 210.25, -30),
                                                          BuildMove(sourceA2.Id, 120.5, 220.25, 45)
                                                        ]
                                                      },
                                                      CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.IsError, Is.False);
                      Assert.That(sourceA1.X, Is.EqualTo(110.5));
                      Assert.That(sourceA1.Y, Is.EqualTo(210.25));
                      Assert.That(sourceA1.Width, Is.EqualTo(90.5));
                      Assert.That(sourceA1.Height, Is.EqualTo(50.25));
                      Assert.That(sourceA1.Rotation, Is.EqualTo(-30));
                      Assert.That(sourceA2.X, Is.EqualTo(120.5));
                      Assert.That(sourceA2.Y, Is.EqualTo(220.25));
                      Assert.That(sourceA2.Rotation, Is.EqualTo(45));
                      Assert.That(sourceA1.Name, Is.EqualTo("A1"));
                      Assert.That(sourceA1.AreaId, Is.EqualTo(_gardenId));
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task MoveAsync_ATableTheFestivalDoesNotHave_IsNotFoundAndMovesNothing()
  {
    var source = BuildNamedTable(_tableId, "A1");
    var missingTableId = Guid.NewGuid();
    A.CallTo(() => _repository.FindTablesAsync(_festivalId, A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<Table>>([source]));

    ErrorOr<Success> moved = await _service.MoveAsync(_festivalId,
                                                      new()
                                                      {
                                                        Tables =
                                                        [
                                                          BuildMove(source.Id, 50, 60, 0),
                                                          BuildMove(missingTableId, 50, 120, 0)
                                                        ]
                                                      },
                                                      CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(moved.RefusalMessageKey(), Is.EqualTo("TableNotFound"));
                      Assert.That(moved.RefusalDescription(), Does.Contain(missingTableId.ToString()));
                      Assert.That(source.X, Is.EqualTo(10));
                      Assert.That(source.Y, Is.EqualTo(20));
                    });

    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task MoveAsync_NoTable_IsRefusedGenericallyWithTheReason()
  {
    ErrorOr<Success> moved = await _service.MoveAsync(_festivalId, new() { Tables = [] }, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(moved.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(moved.RefusalDescription(), Does.Contain("listed no table"));
                    });

    A.CallTo(() => _repository.FindTablesAsync(A<Guid>._, A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task MoveAsync_TheSameTableTwice_IsRefusedGenericallyWithTheReason()
  {
    ErrorOr<Success> moved = await _service.MoveAsync(_festivalId,
                                                      new()
                                                      {
                                                        Tables =
                                                        [
                                                          BuildMove(_tableId, 50, 60, 0),
                                                          BuildMove(_tableId, 50, 120, 0)
                                                        ]
                                                      },
                                                      CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(moved.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(moved.RefusalDescription(), Does.Contain("more than once"));
                    });

    A.CallTo(() => _repository.FindTablesAsync(A<Guid>._, A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  private MoveTableRequest BuildMove(Guid tableId, double x, double y, double rotation)
  {
    return new()
           {
             TableId = tableId,
             Geometry = new()
                        {
                          X = x,
                          Y = y,
                          Width = 90.5,
                          Height = 50.25,
                          Rotation = rotation
                        }
           };
  }

  private CopyTableRequest BuildCopy(Guid sourceTableId, double x, double y)
  {
    return new()
           {
             SourceTableId = sourceTableId,
             Geometry = new()
                        {
                          X = x,
                          Y = y,
                          Width = 80,
                          Height = 40,
                          Rotation = 15.5
                        }
           };
  }

  private Table BuildNamedTable(Guid tableId, string name)
  {
    return new()
           {
             Id = tableId,
             FestivalId = _festivalId,
             AreaId = _gardenId,
             Name = name,
             X = 10,
             Y = 20,
             Width = 80,
             Height = 40,
             Rotation = 0,
             IsHidden = false
           };
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
