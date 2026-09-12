using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop.Tests.Services;

[TestFixture]
public sealed class UpdateInstallGateTests
{
  private readonly DateTime _now = new(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);

  [SetUp]
  public void SetUp()
  {
    _festivals = A.Fake<IFestivalReader>();
    _clock = A.Fake<IClock>();
    A.CallTo(() => _clock.UtcNow).Returns(_now);
    A.CallTo(() => _festivals.ReadAllAsync(A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyCollection<Festival>>([]));
  }

  private IFestivalReader _festivals = null!;
  private IClock _clock = null!;

  private UpdateInstallGate CreateGate()
  {
    return new(_festivals, new FestivalSchedule(), _clock);
  }

  private Festival FestivalOf(DateTime startsAtUtc, DateTime endsAtUtc, bool isHidden = false)
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

  private void FestivalsAre(params Festival[] festivals)
  {
    A.CallTo(() => _festivals.ReadAllAsync(A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyCollection<Festival>>(festivals));
  }

  [Test]
  public async Task CanInstallNowAsync_WhenAFestivalIsRunning_IsFalse()
  {
    FestivalsAre(FestivalOf(_now.AddHours(-1), _now.AddHours(1)));

    Assert.That(await CreateGate().CanInstallNowAsync(CancellationToken.None), Is.False);
  }

  [Test]
  public async Task CanInstallNowAsync_WhenAHiddenFestivalStartsInTwelveHours_IsFalse()
  {
    FestivalsAre(FestivalOf(_now.AddHours(12), _now.AddHours(30), true));

    Assert.That(await CreateGate().CanInstallNowAsync(CancellationToken.None), Is.False);
  }

  [Test]
  public async Task CanInstallNowAsync_WhenTheNextFestivalIsTwentyFiveHoursAway_IsTrue()
  {
    FestivalsAre(FestivalOf(_now.AddHours(25), _now.AddHours(40)));

    Assert.That(await CreateGate().CanInstallNowAsync(CancellationToken.None), Is.True);
  }

  [Test]
  public async Task CanInstallNowAsync_WhenTheLastFestivalAlreadyEnded_IsTrue()
  {
    FestivalsAre(FestivalOf(_now.AddHours(-25), _now.AddHours(-1)));

    Assert.That(await CreateGate().CanInstallNowAsync(CancellationToken.None), Is.True);
  }

  [Test]
  public async Task CanInstallNowAsync_WhenTheFestivalListCannotBeRead_IsFalse()
  {
    A.CallTo(() => _festivals.ReadAllAsync(A<CancellationToken>._))
     .ThrowsAsync(new InvalidOperationException("The server is not running."));

    Assert.That(await CreateGate().CanInstallNowAsync(CancellationToken.None), Is.False);
  }
}
