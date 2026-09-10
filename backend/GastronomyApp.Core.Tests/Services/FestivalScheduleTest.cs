using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class FestivalScheduleTest
{
  private readonly DateTime _start = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);
  private readonly DateTime _end = new(2026, 8, 27, 15, 0, 0, DateTimeKind.Utc);

  private readonly FestivalSchedule _schedule = new();

  private Festival FestivalOf(string name, DateTime startsAtUtc, DateTime endsAtUtc, bool isHidden = false)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             StartsAtUtc = startsAtUtc,
             EndsAtUtc = endsAtUtc,
             NextOrderNumber = 1,
             IsHidden = isHidden
           };
  }

  [Test]
  public void IsRunning_AtTheVeryStart_IsTrueBecauseTheStartCounts()
  {
    Assert.That(_schedule.IsRunning(FestivalOf("Sommerfest", _start, _end), _start), Is.True);
  }

  [Test]
  public void IsRunning_AtTheVeryEnd_IsFalseBecauseTheEndDoesNotCount()
  {
    Assert.That(_schedule.IsRunning(FestivalOf("Sommerfest", _start, _end), _end), Is.False);
  }

  [Test]
  public void IsRunning_HiddenFestivalInsideItsOwnPeriod_IsFalse()
  {
    var hidden = FestivalOf("Sommerfest", _start, _end, true);

    Assert.That(_schedule.IsRunning(hidden, _start.AddHours(3)), Is.False);
  }

  [Test]
  public void RunningAt_OneOfSeveralFestivalsCoversTheMoment_ReturnsThatOne()
  {
    var earlier = FestivalOf("Fruehlingsfest", _start.AddYears(-1), _end.AddYears(-1));
    var now = FestivalOf("Sommerfest", _start, _end);
    var later = FestivalOf("Herbstfest", _start.AddMonths(2), _end.AddMonths(2));

    Assert.That(_schedule.RunningAt([earlier, now, later], _start.AddHours(3)), Is.SameAs(now));
  }

  [Test]
  public void RunningAt_NothingCoversTheMoment_ReturnsNull()
  {
    var festival = FestivalOf("Sommerfest", _start, _end);

    Assert.That(_schedule.RunningAt([festival], _end.AddHours(1)), Is.Null);
  }

  [Test]
  public void Overlapping_PeriodInsideAnotherFestival_ReturnsTheFestivalInTheWay()
  {
    var standing = FestivalOf("Sommerfest", _start, _end);

    var inTheWay = _schedule.Overlapping(Guid.Empty,
                                         _start.AddHours(2),
                                         _start.AddHours(4),
                                         [standing]);

    Assert.That(inTheWay, Is.SameAs(standing));
  }

  [Test]
  public void Overlapping_PeriodThatOnlyTouchesTheEndOfAnother_ReturnsNull()
  {
    var standing = FestivalOf("Sommerfest", _start, _end);

    var inTheWay = _schedule.Overlapping(Guid.Empty, _end, _end.AddHours(4), [standing]);

    Assert.That(inTheWay, Is.Null);
  }

  [Test]
  public void Overlapping_TheCandidateItselfIsHidden_StillReturnsTheFestivalInTheWay()
  {
    var candidate = FestivalOf("Herbstfest", _start, _end, true);
    var standing = FestivalOf("Sommerfest", _start, _end);

    var inTheWay = _schedule.Overlapping(candidate.Id, _start, _end, [candidate, standing]);

    Assert.That(inTheWay, Is.SameAs(standing));
  }

  [Test]
  public void Overlapping_TheOnlyFestivalInTheWayIsHidden_StillReturnsIt()
  {
    var hidden = FestivalOf("Sommerfest", _start, _end, true);

    var inTheWay = _schedule.Overlapping(Guid.Empty, _start, _end, [hidden]);

    Assert.That(inTheWay, Is.SameAs(hidden));
  }

  [Test]
  public void Overlapping_TheCandidateIsTheFestivalBeingEdited_IgnoresItsOwnPeriod()
  {
    var beingEdited = FestivalOf("Sommerfest", _start, _end);

    var inTheWay = _schedule.Overlapping(beingEdited.Id,
                                         _start.AddHours(1),
                                         _end.AddHours(1),
                                         [beingEdited]);

    Assert.That(inTheWay, Is.Null);
  }
}
