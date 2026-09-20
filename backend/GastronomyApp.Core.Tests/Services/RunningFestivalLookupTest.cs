using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class RunningFestivalLookupTest
{
  [SetUp]
  public void SetUp()
  {
    _festivalRepository = A.Fake<IFestivalRepository>();
    _clock = A.Fake<IClock>();

    A.CallTo(() => _clock.UtcNow).Returns(_now);

    _lookup = new(_festivalRepository, new(), _clock);
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");

  private IClock _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private RunningFestivalLookup _lookup = null!;

  [Test]
  public async Task FindAsync_AFestivalCoversThisMoment_AnswersWithThatFestival()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(_now, A<CancellationToken>._))
     .Returns(Task.FromResult<Festival?>(Festival(_now.AddHours(-2), _now.AddHours(5), false)));

    Festival? found = await _lookup.FindAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(found?.Id, Is.EqualTo(_festivalId));
  }

  [Test]
  public async Task FindAsync_NoFestivalCoversThisMoment_AnswersWithNothing()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._))
     .Returns(Task.FromResult<Festival?>(null));

    Festival? found = await _lookup.FindAsync(TestContext.CurrentContext.CancellationToken);

    Assert.That(found, Is.Null);
  }

  [Test]
  public async Task FindAsync_AskedForTheRunningFestival_AsksForTheMomentTheClockShows()
  {
    await _lookup.FindAsync(TestContext.CurrentContext.CancellationToken);

    A.CallTo(() => _festivalRepository.FindRunningAsync(_now, A<CancellationToken>._)).MustHaveHappened();
  }

  [Test]
  public void IsRunning_AFestivalCoveringThisMoment_SaysItIsRunning()
  {
    Assert.That(_lookup.IsRunning(Festival(_now.AddHours(-2), _now.AddHours(5), false)), Is.True);
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

  private Festival Festival(DateTime startsAtUtc, DateTime endsAtUtc, bool isHidden)
  {
    return new()
           {
             Id = _festivalId,
             Name = "Sommerfest",
             StartsAtUtc = startsAtUtc,
             EndsAtUtc = endsAtUtc,
             NextOrderNumber = 1,
             IsHidden = isHidden
           };
  }
}
