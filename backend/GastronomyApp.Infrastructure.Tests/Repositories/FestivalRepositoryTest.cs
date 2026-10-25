using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class FestivalRepositoryTest
{
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
}
