using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class AreaEditServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _clock = new(new(_now));
    await MoveTheFestivalAsync(_now.AddHours(-5), _now.AddHours(5));
    _service = new(_fixture.DbContext, new CoreMappingFactory().Create(), new(), new(_fixture.DbContext, new CoreMappingFactory().Create(), new(new FestivalRepository(_fixture.DbContext, new()), new(), _clock)));
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
  private SeededDomain _seeded = null!;
  private AreaEditService _service = null!;

  [Test]
  public async Task CreateAsync_AFreeName_StoresAShownAreaWithTheNameAndAnswersItsId()
  {
    var created = await _service.CreateAsync(_seeded.FestivalId, BuildRequest("Zelt am See"), TestContext.CurrentContext.CancellationToken);

    _fixture.DbContext.ChangeTracker.Clear();
    var stored = await _fixture.DbContext.Areas.SingleAsync(area => area.Id == created.Value.AreaId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(stored.FestivalId, Is.EqualTo(_seeded.FestivalId));
                      Assert.That(stored.Name, Is.EqualTo("Zelt am See"));
                      Assert.That(stored.IsHidden, Is.False);
                    });
  }

  [Test]
  public async Task CreateAsync_ANameAnotherAreaOfTheFestivalCarriesInOtherCapitals_IsRefusedAsTakenAndStoresNothing()
  {
    await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, true);

    var created = await _service.CreateAsync(_seeded.FestivalId, BuildRequest("GARTEN"), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.areaNameTaken"));
                      Assert.That(created.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("AreaNameTaken"));
                    });
    Assert.That(await _fixture.DbContext.Areas.CountAsync(TestContext.CurrentContext.CancellationToken), Is.EqualTo(1));
  }

  [Test]
  public async Task CreateAsync_TheNameOfAnAreaAtAnotherFestival_IsFree()
  {
    var otherFestivalId = await _plan.AddFestivalAsync(_fixture.DbContext);
    await _plan.AddAreaAsync(_fixture.DbContext, otherFestivalId, "Garten", 1, false);

    var created = await _service.CreateAsync(_seeded.FestivalId, BuildRequest("Garten"), TestContext.CurrentContext.CancellationToken);

    Assert.That(created.IsError, Is.False);
  }

  [Test]
  public async Task CreateAsync_AFestivalThatIsNotThere_IsNotFound()
  {
    var created = await _service.CreateAsync(Guid.NewGuid(), BuildRequest("Garten"), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("FestivalNotFound"));
                    });
  }

  [Test]
  public async Task CreateAsync_AFestivalWithAHiddenAreaLast_NumbersTheNewAreaAfterIt()
  {
    await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Halle", 4, true);

    var created = await _service.CreateAsync(_seeded.FestivalId, BuildRequest("Zelt am See"), TestContext.CurrentContext.CancellationToken);

    Assert.That(await SortOrderOfAsync(created.Value.AreaId), Is.EqualTo(5));
  }

  [Test]
  public async Task CreateAsync_AFestivalWithoutAreas_NumbersTheNewAreaFirst()
  {
    var created = await _service.CreateAsync(_seeded.FestivalId, BuildRequest("Zelt am See"), TestContext.CurrentContext.CancellationToken);

    Assert.That(await SortOrderOfAsync(created.Value.AreaId), Is.EqualTo(1));
  }

  [Test]
  public async Task UpdateAsync_ANewName_StoresItAndKeepsThePlaceOfTheArea()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 3, false);

    var updated = await _service.UpdateAsync(_seeded.FestivalId, gardenId, BuildRequest("garten"), TestContext.CurrentContext.CancellationToken);

    _fixture.DbContext.ChangeTracker.Clear();
    var stored = await _fixture.DbContext.Areas.SingleAsync(area => area.Id == gardenId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.Value.AreaId, Is.EqualTo(gardenId));
                      Assert.That(stored.Name, Is.EqualTo("garten"));
                      Assert.That(stored.SortOrder, Is.EqualTo(3));
                    });
  }

  [Test]
  public async Task UpdateAsync_ANameAnotherAreaCarries_IsRefusedAsTakenAndKeepsTheOldName()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Halle", 2, false);

    var updated = await _service.UpdateAsync(_seeded.FestivalId, gardenId, BuildRequest("halle"), TestContext.CurrentContext.CancellationToken);

    _fixture.DbContext.ChangeTracker.Clear();
    var stored = await _fixture.DbContext.Areas.SingleAsync(area => area.Id == gardenId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.areaNameTaken"));
                      Assert.That(stored.Name, Is.EqualTo("Garten"));
                    });
  }

  [Test]
  public async Task UpdateAsync_TheNameTheAreaAlreadyCarries_IsNotTakenByItself()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);

    var updated = await _service.UpdateAsync(_seeded.FestivalId, gardenId, BuildRequest("Garten"), TestContext.CurrentContext.CancellationToken);

    Assert.That(updated.IsError, Is.False);
  }

  [Test]
  public async Task UpdateAsync_AnAreaTheFestivalDoesNotHave_IsNotFound()
  {
    var otherFestivalId = await _plan.AddFestivalAsync(_fixture.DbContext);
    var strangerAreaId = await _plan.AddAreaAsync(_fixture.DbContext, otherFestivalId, "Garten", 1, false);

    var updated = await _service.UpdateAsync(_seeded.FestivalId, strangerAreaId, BuildRequest("Garten"), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(updated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
                      Assert.That(updated.RefusalMessageKey(), Is.EqualTo("AreaNotFound"));
                      Assert.That(updated.RefusalDescription(), Does.Contain(strangerAreaId.ToString()));
                    });
  }

  [Test]
  public async Task HideAsync_AShownAreaWhileTheFestivalRuns_HidesIt()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);

    var hidden = await _service.HideAsync(_seeded.FestivalId, gardenId, TestContext.CurrentContext.CancellationToken);

    var isHidden = await IsHiddenAsync(gardenId);

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.Value.AreaId, Is.EqualTo(gardenId));
                      Assert.That(isHidden, Is.True);
                    });
  }

  [Test]
  public async Task HideAsync_AnAreaWithItemsStillToHandOutOrSettleWhileTheFestivalRuns_IsRefusedAndStaysShown()
  {
    var gardenId = await AreaWithAnUnsettledItemAsync();

    var hidden = await _service.HideAsync(_seeded.FestivalId, gardenId, TestContext.CurrentContext.CancellationToken);

    var isHidden = await IsHiddenAsync(gardenId);

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.FirstError.NumericType, Is.EqualTo(RefusalType.Conflict));
                      Assert.That(hidden.RefusalMessageKey(), Is.EqualTo("errors.admin.festivals.areaWithItemsToHandOutOrSettleStaysShown"));
                      Assert.That(hidden.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("AreaWithItemsToHandOutOrSettleStaysShown"));
                      Assert.That(isHidden, Is.False);
                    });
  }

  [Test]
  public async Task HideAsync_AnAreaWithItemsStillToHandOutOrSettleAfterTheFestival_HidesIt()
  {
    var gardenId = await AreaWithAnUnsettledItemAsync();
    await MoveTheFestivalAsync(_now.AddHours(-6), _now.AddHours(-1));

    var hidden = await _service.HideAsync(_seeded.FestivalId, gardenId, TestContext.CurrentContext.CancellationToken);

    var isHidden = await IsHiddenAsync(gardenId);

    Assert.Multiple(() =>
                    {
                      Assert.That(hidden.IsError, Is.False);
                      Assert.That(isHidden, Is.True);
                    });
  }

  [Test]
  public async Task HideAsync_AnAreaWhoseItemsAreAllHandedOutAndSettledWhileTheFestivalRuns_HidesIt()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var tableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 1", false);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, tableId, 1, _now, _now);

    var hidden = await _service.HideAsync(_seeded.FestivalId, gardenId, TestContext.CurrentContext.CancellationToken);

    Assert.That(hidden.IsError, Is.False);
  }

  [Test]
  public async Task HideAsync_AnAreaTheFestivalDoesNotHave_IsNotFound()
  {
    var hidden = await _service.HideAsync(_seeded.FestivalId, Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(hidden.RefusalMessageKey(), Is.EqualTo("AreaNotFound"));
  }

  [Test]
  public async Task MoveAsync_AnAreaUp_SwapsItWithTheOneAboveAndNumbersThemFromOne()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var hallId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Halle", 2, false);
    var tentId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Zelt", 3, false);

    var moved = await _service.MoveAsync(_seeded.FestivalId, tentId, BuildMove(MoveDirection.Up, false), TestContext.CurrentContext.CancellationToken);

    var sortOrders = await SortOrdersOfAsync(gardenId, hallId, tentId);

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.Value.AreaId, Is.EqualTo(tentId));
                      Assert.That(sortOrders,
                                  Is.EqualTo(new[]
                                             {
                                               1,
                                               3,
                                               2
                                             }));
                    });
  }

  [Test]
  public async Task MoveAsync_TheFirstAreaUp_ChangesNothing()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var hallId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Halle", 2, false);

    var moved = await _service.MoveAsync(_seeded.FestivalId, gardenId, BuildMove(MoveDirection.Up, false), TestContext.CurrentContext.CancellationToken);

    var sortOrders = await SortOrdersOfAsync(gardenId, hallId);

    Assert.Multiple(() =>
                    {
                      Assert.That(moved.Value.AreaId, Is.EqualTo(gardenId));
                      Assert.That(sortOrders,
                                  Is.EqualTo(new[]
                                             {
                                               1,
                                               2
                                             }));
                    });
  }

  [Test]
  public async Task MoveAsync_AHiddenAreaAboveWhileHiddenAreasAreNotShown_SwapsWithTheShownAreaAboveIt()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var hallId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Halle", 2, true);
    var tentId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Zelt", 3, false);

    await _service.MoveAsync(_seeded.FestivalId, tentId, BuildMove(MoveDirection.Up, false), TestContext.CurrentContext.CancellationToken);

    Assert.That(await SortOrdersOfAsync(gardenId, hallId, tentId),
                Is.EqualTo(new[]
                           {
                             3,
                             2,
                             1
                           }));
  }

  [Test]
  public async Task MoveAsync_AHiddenAreaAboveWhileHiddenAreasAreShown_SwapsWithIt()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var hallId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Halle", 2, true);
    var tentId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Zelt", 3, false);

    await _service.MoveAsync(_seeded.FestivalId, tentId, BuildMove(MoveDirection.Up, true), TestContext.CurrentContext.CancellationToken);

    Assert.That(await SortOrdersOfAsync(gardenId, hallId, tentId),
                Is.EqualTo(new[]
                           {
                             1,
                             3,
                             2
                           }));
  }

  [Test]
  public async Task MoveAsync_OnlyHiddenAreasAboveWhileHiddenAreasAreNotShown_ChangesNothing()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, true);
    var hallId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Halle", 2, false);

    await _service.MoveAsync(_seeded.FestivalId, hallId, BuildMove(MoveDirection.Up, false), TestContext.CurrentContext.CancellationToken);

    Assert.That(await SortOrdersOfAsync(gardenId, hallId),
                Is.EqualTo(new[]
                           {
                             1,
                             2
                           }));
  }

  [Test]
  public async Task MoveAsync_AnAreaTheFestivalDoesNotHave_IsNotFound()
  {
    var moved = await _service.MoveAsync(_seeded.FestivalId, Guid.NewGuid(), BuildMove(MoveDirection.Down, false), TestContext.CurrentContext.CancellationToken);

    Assert.That(moved.RefusalMessageKey(), Is.EqualTo("AreaNotFound"));
  }

  private async Task<Guid> AreaWithAnUnsettledItemAsync()
  {
    var gardenId = await _plan.AddAreaAsync(_fixture.DbContext, _seeded.FestivalId, "Garten", 1, false);
    var tableId = await _plan.AddTableAsync(_fixture.DbContext, _seeded.FestivalId, gardenId, "Tisch 1", false);
    await _plan.AddOrderAsync(_fixture.DbContext, _seeded, tableId, 1, _now, null);

    return gardenId;
  }

  private async Task<bool> IsHiddenAsync(Guid areaId)
  {
    _fixture.DbContext.ChangeTracker.Clear();

    return (await _fixture.DbContext.Areas.SingleAsync(area => area.Id == areaId, TestContext.CurrentContext.CancellationToken)).IsHidden;
  }

  private async Task<int> SortOrderOfAsync(Guid areaId)
  {
    return (await SortOrdersOfAsync(areaId)).Single();
  }

  private async Task<int[]> SortOrdersOfAsync(params Guid[] areaIds)
  {
    _fixture.DbContext.ChangeTracker.Clear();
    var stored = await _fixture.DbContext.Areas.ToListAsync(TestContext.CurrentContext.CancellationToken);

    return areaIds.Select(areaId => stored.Single(area => area.Id == areaId).SortOrder).ToArray();
  }

  private async Task MoveTheFestivalAsync(DateTime startsAtUtc, DateTime endsAtUtc)
  {
    var festival = await _fixture.DbContext.Festivals.SingleAsync(row => row.Id == _seeded.FestivalId, TestContext.CurrentContext.CancellationToken);
    festival.StartsAtUtc = startsAtUtc;
    festival.EndsAtUtc = endsAtUtc;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private MoveAreaRequest BuildMove(MoveDirection direction, bool showsHiddenAreas)
  {
    return new()
           {
             Direction = direction,
             ShowsHiddenAreas = showsHiddenAreas
           };
  }

  private SaveAreaRequest BuildRequest(string name)
  {
    return new() { Name = name };
  }
}
