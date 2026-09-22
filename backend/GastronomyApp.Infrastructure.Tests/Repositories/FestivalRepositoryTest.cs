using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class FestivalRepositoryTest
{
  private readonly FestivalService _festivalService = new();

  private readonly DateTime _start = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

  private async Task<Guid> AddFestivalAsync(GastronomyAppDbContext dbContext, string name, DateTime startsAtUtc, DateTime endsAtUtc, bool isHidden)
  {
    var festivalId = Guid.NewGuid();

    dbContext.Festivals.Add(new()
                            {
                              Id = festivalId,
                              Name = name,
                              StartsAtUtc = startsAtUtc,
                              EndsAtUtc = endsAtUtc,
                              NextOrderNumber = 1,
                              IsHidden = isHidden
                            });

    await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return festivalId;
  }

  [Test]
  public async Task FindRunningAsync_AMomentInsideOneOfSeveralFestivals_ReturnsThatFestival()
  {
    using SqliteInMemoryFixture fixture = new();
    await AddFestivalAsync(fixture.DbContext, "Fruehlingsfest", _start.AddMonths(-3), _start.AddMonths(-3).AddDays(2), false);
    var wantedId = await AddFestivalAsync(fixture.DbContext, "Sommerfest", _start, _start.AddDays(1), false);

    FestivalRepository repository = new(fixture.DbContext, new());

    var found = await repository.FindRunningAsync(_start.AddHours(6), TestContext.CurrentContext.CancellationToken);

    Assert.That(found?.Id, Is.EqualTo(wantedId));
  }

  [Test]
  public async Task FindRunningAsync_TheOnlyFestivalCoveringTheMomentIsHidden_ReturnsNull()
  {
    using SqliteInMemoryFixture fixture = new();
    await AddFestivalAsync(fixture.DbContext, "Sommerfest", _start, _start.AddDays(1), true);

    FestivalRepository repository = new(fixture.DbContext, new());

    var found = await repository.FindRunningAsync(_start.AddHours(6), TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.Null);
  }

  [Test]
  public async Task FindRunningAsync_ReadsAFestivalBack_StampsBothMomentsAsUtc()
  {
    using SqliteInMemoryFixture fixture = new();
    await AddFestivalAsync(fixture.DbContext, "Sommerfest", _start, _start.AddDays(1), false);

    FestivalRepository repository = new(fixture.DbContext, new());

    var found = await repository.FindRunningAsync(_start.AddHours(6), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(found!.StartsAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
                      Assert.That(found.EndsAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
                    });
  }

  [Test]
  public async Task ExistsAsync_AFestivalThatWasStored_AnswersTrue()
  {
    using SqliteInMemoryFixture fixture = new();
    var festivalId = await AddFestivalAsync(fixture.DbContext, "Sommerfest", _start, _start.AddDays(1), false);

    FestivalRepository repository = new(fixture.DbContext, new());

    Assert.Multiple(async () =>
                    {
                      Assert.That(await repository.ExistsAsync(festivalId, TestContext.CurrentContext.CancellationToken), Is.True);
                      Assert.That(await repository.ExistsAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken), Is.False);
                    });
  }

  [Test]
  public async Task FindIdsNotEndedAsync_AFestivalThatIsOverAndAHiddenOne_ReturnsOnlyTheOneStillToCome()
  {
    using SqliteInMemoryFixture fixture = new();
    await AddFestivalAsync(fixture.DbContext, "Fruehlingsfest", _start.AddMonths(-3), _start.AddMonths(-3).AddDays(2), false);
    await AddFestivalAsync(fixture.DbContext, "Verstecktes Fest", _start, _start.AddDays(1), true);
    var wantedId = await AddFestivalAsync(fixture.DbContext, "Sommerfest", _start, _start.AddDays(1), false);

    FestivalRepository repository = new(fixture.DbContext, new());

    IReadOnlyList<Guid> found = await repository.FindIdsNotEndedAsync(_start.AddHours(6), TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.EqualTo(new[] { wantedId }));
  }

  [Test]
  public async Task FindByIdAsync_AFestivalThatIsNotThere_ReturnsNothing()
  {
    using SqliteInMemoryFixture fixture = new();
    await AddFestivalAsync(fixture.DbContext, "Sommerfest", _start, _start.AddDays(1), false);

    FestivalRepository repository = new(fixture.DbContext, new());

    Assert.That(await repository.FindByIdAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken), Is.Null);
  }

  [Test]
  public async Task FindAllWithContentsAsync_AFestivalNothingHasBeenAddedTo_CountsZeroStationsAndItems()
  {
    using SqliteInMemoryFixture fixture = new();
    var festivalId = await AddFestivalAsync(fixture.DbContext, "Sommerfest", _start, _start.AddDays(1), false);

    FestivalRepository repository = new(fixture.DbContext, new());

    IReadOnlyList<Festival> festivals = await repository.FindAllWithContentsAsync(TestContext.CurrentContext.CancellationToken);
    IReadOnlyDictionary<Guid, int> orderCounts = await repository.CountOrdersByFestivalAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(festivals.Select(festival => festival.Id), Is.EqualTo(new[] { festivalId }));
                      Assert.That(_festivalService.StationCountOf(festivals[0]), Is.EqualTo(0));
                      Assert.That(_festivalService.MenuItemCountOf(festivals[0]), Is.EqualTo(0));
                      Assert.That(orderCounts.GetValueOrDefault(festivalId), Is.EqualTo(0));
                    });
  }

  [Test]
  public async Task FindAllWithContentsAsync_AFestivalWithStationsAndAMenu_CountsWhatBelongsToIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    FestivalRepository repository = new(fixture.DbContext, new());

    IReadOnlyList<Festival> festivals = await repository.FindAllWithContentsAsync(TestContext.CurrentContext.CancellationToken);
    IReadOnlyDictionary<Guid, int> orderCounts = await repository.CountOrdersByFestivalAsync(TestContext.CurrentContext.CancellationToken);

    var sommerfest = festivals.First(festival => festival.Id == seeded.FestivalId);

    Assert.Multiple(() =>
                    {
                      Assert.That(_festivalService.StationCountOf(sommerfest), Is.EqualTo(2));
                      Assert.That(_festivalService.MenuItemCountOf(sommerfest), Is.EqualTo(2));
                      Assert.That(orderCounts.GetValueOrDefault(seeded.FestivalId), Is.EqualTo(0));
                    });
  }

  [Test]
  public async Task CopyContentsAsync_AFestivalWithAMenu_CopiesItsStationsItemsAndAssignmentsAcross()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    FestivalRepository repository = new(fixture.DbContext, new());
    var newFestivalId = Guid.NewGuid();

    await repository.AddAsync(new()
                              {
                                Id = newFestivalId,
                                Name = "Sommerfest 2027",
                                StartsAtUtc = _start.AddYears(1),
                                EndsAtUtc = _start.AddYears(1).AddDays(1),
                                NextOrderNumber = 1,
                                IsHidden = false
                              },
                              TestContext.CurrentContext.CancellationToken);
    await repository.CopyContentsAsync(seeded.FestivalId, newFestivalId, TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    IReadOnlyList<Festival> festivals = await repository.FindAllWithContentsAsync(TestContext.CurrentContext.CancellationToken);

    var copy = festivals.First(festival => festival.Id == newFestivalId);

    Assert.Multiple(() =>
                    {
                      Assert.That(_festivalService.StationCountOf(copy), Is.EqualTo(2));
                      Assert.That(_festivalService.MenuItemCountOf(copy), Is.EqualTo(2));
                    });
  }

  [Test]
  public async Task CopyContentsAsync_AFestivalWhoseItemsHaveSoldOut_PutsThemBackOnSaleAtTheCopy()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    foreach (var menuRow in fixture.DbContext.FestivalCatalogItems)
      menuRow.IsAvailable = false;

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    FestivalRepository repository = new(fixture.DbContext, new());
    var newFestivalId = await AddFestivalAsync(fixture.DbContext, "Sommerfest 2027", _start.AddYears(1), _start.AddYears(1).AddDays(1), false);

    await repository.CopyContentsAsync(seeded.FestivalId, newFestivalId, TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();

    Assert.That(readContext.FestivalCatalogItems.Where(menuRow => menuRow.FestivalId == newFestivalId).Select(menuRow => menuRow.IsAvailable), Is.All.True);
  }
}
