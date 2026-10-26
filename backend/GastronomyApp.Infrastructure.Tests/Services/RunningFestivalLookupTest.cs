using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class RunningFestivalLookupTest
{
  [SetUp]
  public void SetUp()
  {
    _fixture = new();
    _clock = new(new DateTimeOffset(_now));
    _lookup = new(_fixture.DbContext, new(), _clock);
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);

  private FakeTimeProvider _clock = null!;
  private SqliteInMemoryFixture _fixture = null!;
  private RunningFestivalLookup _lookup = null!;

  [Test]
  public async Task FindAsync_AMomentInsideOneOfSeveralFestivals_AnswersWithThatFestival()
  {
    await AddFestivalAsync("Fruehlingsfest", _now.AddMonths(-3), _now.AddMonths(-3).AddDays(2), false);
    var wantedId = await AddFestivalAsync("Sommerfest", _now.AddHours(-2), _now.AddHours(5), false);

    var found = await _lookup.FindAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(found?.Id, Is.EqualTo(wantedId));
  }

  [Test]
  public async Task FindAsync_NoFestivalCoversThisMoment_AnswersWithNothing()
  {
    await AddFestivalAsync("Sommerfest", _now.AddHours(-8), _now.AddHours(-1), false);

    var found = await _lookup.FindAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.Null);
  }

  [Test]
  public async Task FindAsync_TheOnlyFestivalCoveringThisMomentIsHidden_AnswersWithNothing()
  {
    await AddFestivalAsync("Sommerfest", _now.AddHours(-2), _now.AddHours(5), true);

    var found = await _lookup.FindAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.Null);
  }

  [Test]
  public async Task FindAsync_TheClockMovesOnAfterTheFestivalEnded_AnswersWithNothing()
  {
    await AddFestivalAsync("Sommerfest", _now.AddHours(-2), _now.AddHours(5), false);
    _clock.Advance(TimeSpan.FromHours(5));

    var found = await _lookup.FindAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.Null);
  }

  [Test]
  public async Task FindAsync_ReadsAFestivalBack_StampsBothMomentsAsUtc()
  {
    await AddFestivalAsync("Sommerfest", _now.AddHours(-2), _now.AddHours(5), false);

    var found = await _lookup.FindAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(found!.StartsAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
                      Assert.That(found.EndsAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
                    });
  }

  [Test]
  public void IsRunning_AFestivalCoveringThisMoment_SaysItIsRunning()
  {
    Assert.That(_lookup.IsRunning(Festival(_now.AddHours(-2), _now.AddHours(5), false)), Is.True);
  }

  [Test]
  public void IsRunning_ByItsFieldsForAFestivalCoveringThisMoment_SaysItIsRunningUnlessItIsHidden()
  {
    Assert.Multiple(() =>
                    {
                      Assert.That(_lookup.IsRunning(false, _now.AddHours(-2), _now.AddHours(5)), Is.True);
                      Assert.That(_lookup.IsRunning(true, _now.AddHours(-2), _now.AddHours(5)), Is.False);
                      Assert.That(_lookup.IsRunning(false, _now.AddHours(-8), _now.AddHours(-1)), Is.False);
                    });
  }

  [Test]
  public void IsRunning_AFestivalThatHasAlreadyEnded_SaysItIsNotRunning()
  {
    Assert.That(_lookup.IsRunning(Festival(_now.AddHours(-8), _now.AddHours(-1), false)), Is.False);
  }

  [Test]
  public void IsRunning_AHiddenFestivalCoveringThisMoment_SaysItIsNotRunning()
  {
    Assert.That(_lookup.IsRunning(Festival(_now.AddHours(-2), _now.AddHours(5), true)), Is.False);
  }

  [Test]
  public void IsRunning_NoFestival_ThrowsArgumentNullException()
  {
    Assert.That(() => _lookup.IsRunning(null!), Throws.ArgumentNullException);
  }

  private async Task<Guid> AddFestivalAsync(string name, DateTime startsAtUtc, DateTime endsAtUtc, bool isHidden)
  {
    var festival = Festival(startsAtUtc, endsAtUtc, isHidden);
    festival.Name = name;
    _fixture.DbContext.Festivals.Add(festival);

    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return festival.Id;
  }

  private static Festival Festival(DateTime startsAtUtc, DateTime endsAtUtc, bool isHidden)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = "Sommerfest",
             StartsAtUtc = startsAtUtc,
             EndsAtUtc = endsAtUtc,
             NextOrderNumber = 1,
             IsHidden = isHidden
           };
  }
}
