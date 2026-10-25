using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Contracts.TablePlan;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class TableEditServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _clock = new(new(_now));
    await MoveTheFestivalAsync(_now.AddHours(-5), _now.AddHours(5));
    _gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    _hallId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Halle", 2, false);
    _service = new(_fixture.DbContext, new CoreMappingFactory().Create(), new(_fixture.DbContext, new CoreMappingFactory().Create(), new(new FestivalRepository(_fixture.DbContext, new()), new(), _clock)), new(), new());
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);
  private readonly TablePlanSeeder _plan = new();

  private FakeTimeProvider _clock = null!;
  private SqliteInMemoryFixture _fixture = null!;
  private Guid _gardenId;
  private Guid _hallId;
  private SeededDomain _seeded = null!;
  private TableEditService _service = null!;

  [Test]
  public async Task CreateAsync_AGeometryAsKonvaHandsItOver_StoresItExactly()
  {
    var created = await _service.CreateAsync(_seeded.FestivalId,
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
                                             TestContext.CurrentContext.CancellationToken);

    var stored = await StoredTableAsync(created.Value.TableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(stored.FestivalId, Is.EqualTo(_seeded.FestivalId));
                      Assert.That(stored.AreaId, Is.EqualTo(_gardenId));
                      Assert.That(stored.Name, Is.EqualTo("Tisch 1"));
                      Assert.That(stored.X, Is.EqualTo(-12.3456789));
                      Assert.That(stored.Y, Is.EqualTo(1234.5));
                      Assert.That(stored.Width, Is.EqualTo(0.1));
                      Assert.That(stored.Height, Is.EqualTo(98765.4321));
                      Assert.That(stored.Rotation, Is.EqualTo(-179.99));
                      Assert.That(stored.IsHidden, Is.False);
                    });
  }

  [Test]
  public async Task CreateAsync_AnAreaOfAnotherFestival_IsRefusedGenericallyAndStoresNothing()
  {
    var otherFestivalId = await _plan.AddFestivalAsync(_fixture.DbContext);
    var strangerAreaId = await _plan.AddAreaAsync(_fixture.DbContext, otherFestivalId, "Wiese", 1, false);

    var created = await _service.CreateAsync(_seeded.FestivalId, BuildSave(strangerAreaId, "Tisch 1"), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                    });
    Assert.That(await _fixture.DbContext.Tables.CountAsync(TestContext.CurrentContext.CancellationToken), Is.Zero);
  }

  [Test]
  public async Task CreateAsync_ANameAHiddenTableInAnotherAreaCarriesInOtherCapitals_IsRefusedAsTaken()
  {
    await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _hallId, "Tisch 1", true);

    var created = await _service.CreateAsync(_seeded.FestivalId, BuildSave(_gardenId, "TISCH 1"), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.tableNameTaken"));
                      Assert.That(created.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("TableNameTaken"));
                    });
  }

  [Test]
  public async Task UpdateAsync_AnotherAreaOfTheFestival_MovesTheTableThereWithItsNewGeometry()
  {
    var tableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "Tisch 1", false);

    var updated = await _service.UpdateAsync(_seeded.FestivalId,
                                             tableId,
                                             new()
                                             {
                                               AreaId = _hallId,
                                               Name = "Tisch 9",
                                               Geometry = BuildGeometry(5.5, 6.5)
                                             },
                                             TestContext.CurrentContext.CancellationToken);

    var stored = await StoredTableAsync(tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.Value.TableId, Is.EqualTo(tableId));
                      Assert.That(stored.AreaId, Is.EqualTo(_hallId));
                      Assert.That(stored.Name, Is.EqualTo("Tisch 9"));
                      Assert.That(stored.X, Is.EqualTo(5.5));
                      Assert.That(stored.Y, Is.EqualTo(6.5));
                    });
  }

  [Test]
  public async Task UpdateAsync_TheNameTheTableAlreadyCarries_IsNotTakenByItself()
  {
    var tableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "Tisch 1", false);

    var updated = await _service.UpdateAsync(_seeded.FestivalId, tableId, BuildSave(_gardenId, "tisch 1"), TestContext.CurrentContext.CancellationToken);

    Assert.That(updated.IsError, Is.False);
  }

  [Test]
  public async Task UpdateAsync_ANameAnotherTableCarries_IsRefusedAsTakenAndKeepsTheOldName()
  {
    var tableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "Tisch 1", false);
    await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "Tisch 2", false);

    var updated = await _service.UpdateAsync(_seeded.FestivalId, tableId, BuildSave(_gardenId, "Tisch 2"), TestContext.CurrentContext.CancellationToken);

    var stored = await StoredTableAsync(tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.tableNameTaken"));
                      Assert.That(stored.Name, Is.EqualTo("Tisch 1"));
                    });
  }

  [Test]
  public async Task UpdateAsync_AnAreaOfAnotherFestival_IsRefusedGenericallyAndMovesNothing()
  {
    var tableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "Tisch 1", false);
    var otherFestivalId = await _plan.AddFestivalAsync(_fixture.DbContext);
    var strangerAreaId = await _plan.AddAreaAsync(_fixture.DbContext, otherFestivalId, "Wiese", 1, false);

    var updated = await _service.UpdateAsync(_seeded.FestivalId, tableId, BuildSave(strangerAreaId, "Tisch 1"), TestContext.CurrentContext.CancellationToken);

    var stored = await StoredTableAsync(tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(stored.AreaId, Is.EqualTo(_gardenId));
                    });
  }

  [Test]
  public async Task UpdateAsync_ATableTheFestivalDoesNotHave_IsNotFound()
  {
    var updated = await _service.UpdateAsync(_seeded.FestivalId, Guid.NewGuid(), BuildSave(_gardenId, "Tisch 1"), TestContext.CurrentContext.CancellationToken);

    Assert.That(updated.RefusalMessageKey(), Is.EqualTo("TableNotFound"));
  }

  [Test]
  public async Task HideAsync_AShownTableWhileTheFestivalRuns_HidesIt()
  {
    var tableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "Tisch 1", false);

    var hidden = await _service.HideAsync(_seeded.FestivalId, tableId, TestContext.CurrentContext.CancellationToken);

    var stored = await StoredTableAsync(tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.Value.TableId, Is.EqualTo(tableId));
                      Assert.That(stored.IsHidden, Is.True);
                    });
  }

  [Test]
  public async Task HideAsync_ATableWithItemsStillToHandOutOrSettleWhileTheFestivalRuns_IsRefusedAndStaysShown()
  {
    var tableId = await TableWithAnUnsettledItemAsync();

    var hidden = await _service.HideAsync(_seeded.FestivalId, tableId, TestContext.CurrentContext.CancellationToken);

    var stored = await StoredTableAsync(tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(hidden.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.tableWithItemsToHandOutOrSettleStaysShown"));
                      Assert.That(hidden.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("TableWithItemsToHandOutOrSettleStaysShown"));
                      Assert.That(stored.IsHidden, Is.False);
                    });
  }

  [Test]
  public async Task HideAsync_ATableWithItemsStillToHandOutOrSettleAfterTheFestival_HidesIt()
  {
    var tableId = await TableWithAnUnsettledItemAsync();
    await MoveTheFestivalAsync(_now.AddHours(-6), _now.AddHours(-1));

    var hidden = await _service.HideAsync(_seeded.FestivalId, tableId, TestContext.CurrentContext.CancellationToken);

    var stored = await StoredTableAsync(tableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.IsError, Is.False);
                      Assert.That(stored.IsHidden, Is.True);
                    });
  }

  [Test]
  public async Task HideAsync_ATableAlreadyHiddenWithItemsToHandOutOrSettleWhileTheFestivalRuns_AnswersTheHiddenTable()
  {
    var tableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "Tisch 1", true);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, tableId, 1, _now, null);

    var hidden = await _service.HideAsync(_seeded.FestivalId, tableId, TestContext.CurrentContext.CancellationToken);

    Assert.That(hidden.Value.TableId, Is.EqualTo(tableId));
  }

  [Test]
  public async Task HideAsync_ATableTheFestivalDoesNotHave_IsNotFound()
  {
    var hidden = await _service.HideAsync(_seeded.FestivalId, Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(hidden.RefusalMessageKey(), Is.EqualTo("TableNotFound"));
  }

  [Test]
  public async Task CopyAsync_TwoTablesOfTheFestival_CreatesBothWithTheNextFreeNamesAndTheirGeometryInTheRequestedOrder()
  {
    var sourceA1 = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "A1", false);
    var sourceA2 = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "A2", false);

    var copies = await _service.CopyAsync(_seeded.FestivalId,
                                          new()
                                          {
                                            AreaId = _hallId,
                                            Tables =
                                            [
                                              BuildCopy(sourceA2, 200.5, 300.25),
                                              BuildCopy(sourceA1, 100.5, 200.25)
                                            ]
                                          },
                                          TestContext.CurrentContext.CancellationToken);

    var first = await StoredTableAsync(copies.Value.TableIds[0]);
    var second = await StoredTableAsync(copies.Value.TableIds[1]);

    Assert.Multiple(() =>
                    {
                      Assert.That(copies.Value.TableIds, Has.Count.EqualTo(2));
                      Assert.That(copies.Value.TableIds, Is.All.Not.EqualTo(sourceA1).And.Not.EqualTo(sourceA2));
                      Assert.That(new[]
                                  {
                                    first.Name,
                                    second.Name
                                  },
                                  Is.EqualTo(new[]
                                             {
                                               "A4",
                                               "A3"
                                             }));
                      Assert.That(new[]
                                  {
                                    first.AreaId,
                                    second.AreaId
                                  },
                                  Is.All.EqualTo(_hallId));
                      Assert.That(new[]
                                  {
                                    first.FestivalId,
                                    second.FestivalId
                                  },
                                  Is.All.EqualTo(_seeded.FestivalId));
                      Assert.That(new[]
                                  {
                                    first.IsHidden,
                                    second.IsHidden
                                  },
                                  Is.All.False);
                      Assert.That(first.X, Is.EqualTo(200.5));
                      Assert.That(first.Y, Is.EqualTo(300.25));
                      Assert.That(first.Width, Is.EqualTo(80));
                      Assert.That(first.Height, Is.EqualTo(40));
                      Assert.That(first.Rotation, Is.EqualTo(15.5));
                      Assert.That(second.X, Is.EqualTo(100.5));
                      Assert.That(second.Y, Is.EqualTo(200.25));
                    });
  }

  [Test]
  public async Task CopyAsync_ANameAHiddenTableCarries_IsSkippedForTheCopy()
  {
    var source = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "A1", false);
    await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "a2", true);

    var copies = await _service.CopyAsync(_seeded.FestivalId,
                                          new()
                                          {
                                            AreaId = _hallId,
                                            Tables = [BuildCopy(source, 10, 20)]
                                          },
                                          TestContext.CurrentContext.CancellationToken);

    var stored = await StoredTableAsync(copies.Value.TableIds.Single());

    Assert.That(stored.Name, Is.EqualTo("A3"));
  }

  [Test]
  public async Task CopyAsync_ASourceTheFestivalDoesNotHave_IsNotFoundAndCreatesNothing()
  {
    var source = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "A1", false);
    var missingTableId = Guid.NewGuid();

    var copies = await _service.CopyAsync(_seeded.FestivalId,
                                          new()
                                          {
                                            AreaId = _hallId,
                                            Tables =
                                            [
                                              BuildCopy(source, 10, 20),
                                              BuildCopy(missingTableId, 10, 80)
                                            ]
                                          },
                                          TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(copies.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(copies.RefusalMessageKey(), Is.EqualTo("TableNotFound"));
                      Assert.That(copies.RefusalDescription(), Does.Contain(missingTableId.ToString()));
                    });
    Assert.That(await _fixture.DbContext.Tables.CountAsync(TestContext.CurrentContext.CancellationToken), Is.EqualTo(1));
  }

  [Test]
  public async Task CopyAsync_ASourceOfAnotherFestival_IsNotFoundAndCreatesNothing()
  {
    var otherFestivalId = await _plan.AddFestivalAsync(_fixture.DbContext);
    var strangerAreaId = await _plan.AddAreaAsync(_fixture.DbContext, otherFestivalId, "Wiese", 1, false);
    var strangerTableId = await _plan.AddTableAsync(_fixture.DbContext, otherFestivalId, strangerAreaId, "A1", false);

    var copies = await _service.CopyAsync(_seeded.FestivalId,
                                          new()
                                          {
                                            AreaId = _hallId,
                                            Tables = [BuildCopy(strangerTableId, 10, 20)]
                                          },
                                          TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(copies.RefusalMessageKey(), Is.EqualTo("TableNotFound"));
                    });
    Assert.That(await _fixture.DbContext.Tables.CountAsync(TestContext.CurrentContext.CancellationToken), Is.EqualTo(1));
  }

  [Test]
  public async Task CopyAsync_ATargetAreaOfAnotherFestival_IsRefusedGenericallyAndCreatesNothing()
  {
    var source = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "A1", false);
    var otherFestivalId = await _plan.AddFestivalAsync(_fixture.DbContext);
    var strangerAreaId = await _plan.AddAreaAsync(_fixture.DbContext, otherFestivalId, "Wiese", 1, false);

    var copies = await _service.CopyAsync(_seeded.FestivalId,
                                          new()
                                          {
                                            AreaId = strangerAreaId,
                                            Tables = [BuildCopy(source, 10, 20)]
                                          },
                                          TestContext.CurrentContext.CancellationToken);

    Assert.That(copies.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
    Assert.That(await _fixture.DbContext.Tables.CountAsync(TestContext.CurrentContext.CancellationToken), Is.EqualTo(1));
  }

  [Test]
  public async Task CopyAsync_AGeneratedNameLongerThanFortyCharacters_IsRefusedAndCreatesNothing()
  {
    var fortyCharacterName = new string('T', 40);
    var source = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, fortyCharacterName, false);

    var copies = await _service.CopyAsync(_seeded.FestivalId,
                                          new()
                                          {
                                            AreaId = _hallId,
                                            Tables = [BuildCopy(source, 10, 20)]
                                          },
                                          TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(copies.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(copies.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.tableCopyNameTooLong"));
                      Assert.That(copies.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("TableCopyNameTooLong"));
                      Assert.That(copies.RefusalDescription(), Does.Contain($"{fortyCharacterName} 1"));
                    });
    Assert.That(await _fixture.DbContext.Tables.CountAsync(TestContext.CurrentContext.CancellationToken), Is.EqualTo(1));
  }

  [Test]
  public async Task CopyAsync_NoSourceTable_IsRefusedGenericallyAndCreatesNothing()
  {
    var copies = await _service.CopyAsync(_seeded.FestivalId,
                                          new()
                                          {
                                            AreaId = _hallId,
                                            Tables = []
                                          },
                                          TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(copies.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(copies.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(copies.RefusalDescription(), Does.Contain("no source table"));
                    });
    Assert.That(await _fixture.DbContext.Tables.CountAsync(TestContext.CurrentContext.CancellationToken), Is.Zero);
  }

  [Test]
  public async Task CopyAsync_TheSameSourceTableTwice_IsRefusedGenericallyAndCreatesNothing()
  {
    var source = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "A1", false);

    var copies = await _service.CopyAsync(_seeded.FestivalId,
                                          new()
                                          {
                                            AreaId = _hallId,
                                            Tables =
                                            [
                                              BuildCopy(source, 10, 20),
                                              BuildCopy(source, 10, 80)
                                            ]
                                          },
                                          TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(copies.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(copies.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(copies.RefusalDescription(), Does.Contain("more than once"));
                    });
    Assert.That(await _fixture.DbContext.Tables.CountAsync(TestContext.CurrentContext.CancellationToken), Is.EqualTo(1));
  }

  [Test]
  public async Task MoveAsync_TwoTablesOfTheFestival_ReshapesBothInOneSave()
  {
    var firstId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "A1", false);
    var secondId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "A2", false);
    SaveCounter counter = new();
    await using GastronomyAppDbContext countingContext = new(new DbContextOptionsBuilder<GastronomyAppDbContext>().UseSqlite(_fixture.Connection).AddInterceptors(counter).Options);
    TableEditService countedService = new(countingContext, new CoreMappingFactory().Create(), new(countingContext, new CoreMappingFactory().Create(), new(new FestivalRepository(countingContext, new()), new(), _clock)), new(), new());

    var moved = await countedService.MoveAsync(_seeded.FestivalId,
                                               new()
                                               {
                                                 Tables =
                                                 [
                                                   BuildMove(firstId, 11.5, 22.5),
                                                   BuildMove(secondId, 33.5, 44.5)
                                                 ]
                                               },
                                               TestContext.CurrentContext.CancellationToken);

    var first = await StoredTableAsync(firstId);
    var second = await StoredTableAsync(secondId);

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.IsError, Is.False);
                      Assert.That(new[]
                                  {
                                    first.X,
                                    first.Y
                                  },
                                  Is.EqualTo(new[]
                                             {
                                               11.5,
                                               22.5
                                             }));
                      Assert.That(new[]
                                  {
                                    second.X,
                                    second.Y
                                  },
                                  Is.EqualTo(new[]
                                             {
                                               33.5,
                                               44.5
                                             }));
                      Assert.That(counter.SaveCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task MoveAsync_ATableTheFestivalDoesNotHave_IsNotFoundAndMovesNothing()
  {
    var firstId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "A1", false);
    var missingTableId = Guid.NewGuid();

    var moved = await _service.MoveAsync(_seeded.FestivalId,
                                         new()
                                         {
                                           Tables =
                                           [
                                             BuildMove(firstId, 11.5, 22.5),
                                             BuildMove(missingTableId, 33.5, 44.5)
                                           ]
                                         },
                                         TestContext.CurrentContext.CancellationToken);

    var first = await StoredTableAsync(firstId);

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(moved.RefusalMessageKey(), Is.EqualTo("TableNotFound"));
                      Assert.That(moved.RefusalDescription(), Does.Contain(missingTableId.ToString()));
                      Assert.That(first.X, Is.EqualTo(10));
                    });
  }

  [Test]
  public async Task MoveAsync_NoTable_IsRefusedGenericallyWithTheReason()
  {
    var moved = await _service.MoveAsync(_seeded.FestivalId, new() { Tables = [] }, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(moved.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(moved.RefusalDescription(), Does.Contain("no table"));
                    });
  }

  [Test]
  public async Task MoveAsync_TheSameTableTwice_IsRefusedGenericallyAndMovesNothing()
  {
    var firstId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "A1", false);

    var moved = await _service.MoveAsync(_seeded.FestivalId,
                                         new()
                                         {
                                           Tables =
                                           [
                                             BuildMove(firstId, 11.5, 22.5),
                                             BuildMove(firstId, 33.5, 44.5)
                                           ]
                                         },
                                         TestContext.CurrentContext.CancellationToken);

    var first = await StoredTableAsync(firstId);

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.RefusalMessageKey(), Is.EqualTo("errors.admin.actionFailed"));
                      Assert.That(moved.RefusalDescription(), Does.Contain("more than once"));
                      Assert.That(first.X, Is.EqualTo(10));
                    });
  }

  private async Task<Guid> TableWithAnUnsettledItemAsync()
  {
    var tableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, _gardenId, "Tisch 1", false);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, tableId, 1, _now, null);

    return tableId;
  }

  private async Task<Table> StoredTableAsync(Guid tableId)
  {
    await using var readContext = _fixture.CreateContext();

    return await readContext.Tables.SingleAsync(table => table.Id == tableId, TestContext.CurrentContext.CancellationToken);
  }

  private async Task MoveTheFestivalAsync(DateTime startsAtUtc, DateTime endsAtUtc)
  {
    var festival = await _fixture.DbContext.Festivals.SingleAsync(row => row.Id == _seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    festival.StartsAtUtc = startsAtUtc;
    festival.EndsAtUtc = endsAtUtc;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private SaveTableRequest BuildSave(Guid areaId, string name)
  {
    return new()
           {
             AreaId = areaId,
             Name = name,
             Geometry = BuildGeometry(1, 2)
           };
  }

  private CopyTableRequest BuildCopy(Guid sourceTableId, double x, double y)
  {
    return new()
           {
             SourceTableId = sourceTableId,
             Geometry = BuildGeometry(x, y)
           };
  }

  private MoveTableRequest BuildMove(Guid tableId, double x, double y)
  {
    return new()
           {
             TableId = tableId,
             Geometry = BuildGeometry(x, y)
           };
  }

  private AreaShapeGeometry BuildGeometry(double x, double y)
  {
    return new()
           {
             X = x,
             Y = y,
             Width = 80,
             Height = 40,
             Rotation = 15.5
           };
  }

  private sealed class SaveCounter : SaveChangesInterceptor
  {
    public int SaveCount { get; private set; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
      SaveCount++;

      return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
  }
}
