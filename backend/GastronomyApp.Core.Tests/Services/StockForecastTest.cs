using GastronomyApp.Contracts.Admin.Ingredients;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class StockForecastTest
{
  [SetUp]
  public void SetUp()
  {
    _festival = new()
                {
                  Id = Guid.NewGuid(),
                  Name = "Sommerfest",
                  StartsAtUtc = _now.AddHours(-2),
                  EndsAtUtc = _now.AddHours(6),
                  NextOrderNumber = 1,
                  IsHidden = false
                };

    _forecast = new(new(), new());
  }

  private readonly DateTime _now = new(2026, 9, 26, 20, 0, 0, DateTimeKind.Utc);

  private Festival _festival = null!;
  private StockForecast _forecast = null!;

  [Test]
  public void UsageOf_NoOrderYet_RunsFromZeroAtTheStartToZeroNow()
  {
    IReadOnlyList<IngredientUsagePoint> usage = _forecast.UsageOf(_festival, new Dictionary<DateTime, double>(), _now);

    Assert.That(usage,
                Is.EqualTo(new IngredientUsagePoint[]
                           {
                             new(_now.AddHours(-2), 0),
                             new(_now, 0)
                           }));
  }

  [Test]
  public void UsageOf_MomentsListedOutOfOrder_AddsThemUpInTheOrderTheyHappened()
  {
    Dictionary<DateTime, double> consumedAtOrderTimes = new()
                                                        {
                                                          [_now.AddMinutes(-30)] = 200,
                                                          [_now.AddMinutes(-90)] = 100
                                                        };

    IReadOnlyList<IngredientUsagePoint> usage = _forecast.UsageOf(_festival, consumedAtOrderTimes, _now);

    Assert.That(usage,
                Is.EqualTo(new IngredientUsagePoint[]
                           {
                             new(_now.AddHours(-2), 0),
                             new(_now.AddMinutes(-90), 100),
                             new(_now.AddMinutes(-30), 300),
                             new(_now, 300)
                           }));
  }

  [Test]
  public void UsageOf_BeforeTheFestivalStarts_IsTheStartAlone()
  {
    _festival.StartsAtUtc = _now.AddHours(1);

    IReadOnlyList<IngredientUsagePoint> usage = _forecast.UsageOf(_festival, new Dictionary<DateTime, double>(), _now);

    Assert.That(usage, Is.EqualTo(new IngredientUsagePoint[] { new(_now.AddHours(1), 0) }));
  }

  [Test]
  public void UsageOf_AfterTheFestivalEnded_EndsAtTheFestivalsEndWithTheUsedAmount()
  {
    _festival.StartsAtUtc = _now.AddHours(-10);
    _festival.EndsAtUtc = _now.AddHours(-1);

    IReadOnlyList<IngredientUsagePoint> usage = _forecast.UsageOf(_festival, new Dictionary<DateTime, double> { [_now.AddHours(-5)] = 100 }, _now);

    Assert.That(usage,
                Is.EqualTo(new IngredientUsagePoint[]
                           {
                             new(_now.AddHours(-10), 0),
                             new(_now.AddHours(-5), 100),
                             new(_now.AddHours(-1), 100)
                           }));
  }

  [Test]
  public void UsageOf_OrdersBeforeTheStart_CountsThemInOnePointAtTheStart()
  {
    Dictionary<DateTime, double> consumedAtOrderTimes = new()
                                                        {
                                                          [_now.AddHours(-3)] = 50,
                                                          [_now.AddMinutes(-150)] = 30,
                                                          [_now.AddHours(-1)] = 100
                                                        };

    IReadOnlyList<IngredientUsagePoint> usage = _forecast.UsageOf(_festival, consumedAtOrderTimes, _now);

    Assert.That(usage,
                Is.EqualTo(new IngredientUsagePoint[]
                           {
                             new(_now.AddHours(-2), 80),
                             new(_now.AddHours(-1), 180),
                             new(_now, 180)
                           }));
  }

  [Test]
  public void UsageOf_AnOrderAfterTheEnd_CountsItInTheOnePointAtTheEnd()
  {
    _festival.StartsAtUtc = _now.AddHours(-10);
    _festival.EndsAtUtc = _now.AddHours(-1);
    Dictionary<DateTime, double> consumedAtOrderTimes = new()
                                                        {
                                                          [_now.AddHours(-5)] = 100,
                                                          [_now.AddMinutes(-30)] = 50
                                                        };

    IReadOnlyList<IngredientUsagePoint> usage = _forecast.UsageOf(_festival, consumedAtOrderTimes, _now);

    Assert.That(usage,
                Is.EqualTo(new IngredientUsagePoint[]
                           {
                             new(_now.AddHours(-10), 0),
                             new(_now.AddHours(-5), 100),
                             new(_now.AddHours(-1), 150)
                           }));
  }

  [Test]
  public void UsageOf_OrdersAtAFestivalThatHasNotStarted_CountsThemInTheOnePointAtTheStart()
  {
    _festival.StartsAtUtc = _now.AddHours(1);
    Dictionary<DateTime, double> consumedAtOrderTimes = new()
                                                        {
                                                          [_now.AddHours(-30)] = 40,
                                                          [_now.AddHours(-29)] = 60
                                                        };

    IReadOnlyList<IngredientUsagePoint> usage = _forecast.UsageOf(_festival, consumedAtOrderTimes, _now);

    Assert.That(usage, Is.EqualTo(new IngredientUsagePoint[] { new(_now.AddHours(1), 100) }));
  }

  [Test]
  public void UsageOf_AnOrderAtThisVeryMoment_EndsWithOnePointForNow()
  {
    IReadOnlyList<IngredientUsagePoint> usage = _forecast.UsageOf(_festival, new Dictionary<DateTime, double> { [_now] = 20 }, _now);

    Assert.That(usage,
                Is.EqualTo(new IngredientUsagePoint[]
                           {
                             new(_now.AddHours(-2), 0),
                             new(_now, 20)
                           }));
  }

  [Test]
  public void RunsOutAtUtcSinceStart_HalfUsedAfterTwoHours_RunsOutTwoHoursFromNow()
  {
    Assert.That(_forecast.RunsOutAtUtcSinceStart(_festival, 600, 300, _now), Is.EqualTo(_now.AddHours(2)));
  }

  [Test]
  public void RunsOutAtUtcSinceStart_NoLimit_IsUnknown()
  {
    Assert.That(_forecast.RunsOutAtUtcSinceStart(_festival, null, 300, _now), Is.Null);
  }

  [Test]
  public void RunsOutAtUtcSinceStart_NothingUsedYet_IsUnknown()
  {
    Assert.That(_forecast.RunsOutAtUtcSinceStart(_festival, 600, 0, _now), Is.Null);
  }

  [TestCase(300d)]
  [TestCase(200d)]
  public void RunsOutAtUtcSinceStart_NothingLeft_IsUnknown(double availableAmount)
  {
    Assert.That(_forecast.RunsOutAtUtcSinceStart(_festival, availableAmount, 300, _now), Is.Null);
  }

  [TestCase(1d, 6d)]
  [TestCase(-8d, -1d)]
  public void RunsOutAtUtcSinceStart_FestivalNotRunningAtThisTime_IsUnknown(double startsInHours, double endsInHours)
  {
    _festival.StartsAtUtc = _now.AddHours(startsInHours);
    _festival.EndsAtUtc = _now.AddHours(endsInHours);

    Assert.That(_forecast.RunsOutAtUtcSinceStart(_festival, 600, 300, _now), Is.Null);
  }

  [Test]
  public void RunsOutAtUtcSinceStart_HiddenFestival_IsUnknown()
  {
    _festival.IsHidden = true;

    Assert.That(_forecast.RunsOutAtUtcSinceStart(_festival, 600, 300, _now), Is.Null);
  }

  [Test]
  public void RunsOutAtUtcSinceStart_AnAmountThatLastsFarIntoTheFuture_IsThatMoment()
  {
    Assert.That(_forecast.RunsOutAtUtcSinceStart(_festival, 30_000_001, 1, _now), Is.EqualTo(_now + TimeSpan.FromHours(60_000_000)));
  }

  [TestCase(40_000_001d)]
  [TestCase(1e15)]
  public void RunsOutAtUtcSinceStart_AMomentBeyondTheLastRepresentableDate_IsUnknown(double availableAmount)
  {
    Assert.That(_forecast.RunsOutAtUtcSinceStart(_festival, availableAmount, 1, _now), Is.Null);
  }

  [Test]
  public void RunsOutAtUtcFromLastTwoHours_AnAmountThatLastsFarIntoTheFuture_IsThatMoment()
  {
    IngredientUsagePoint[] usage =
    [
      new(_now.AddHours(-2), 0),
      new(_now.AddHours(-1), 1),
      new(_now, 1)
    ];

    Assert.That(_forecast.RunsOutAtUtcFromLastTwoHours(_festival, 30_000_001, usage, _now), Is.EqualTo(_now + TimeSpan.FromHours(60_000_000)));
  }

  [TestCase(40_000_001d)]
  [TestCase(1e15)]
  public void RunsOutAtUtcFromLastTwoHours_AMomentBeyondTheLastRepresentableDate_IsUnknown(double availableAmount)
  {
    IngredientUsagePoint[] usage =
    [
      new(_now.AddHours(-2), 0),
      new(_now.AddHours(-1), 1),
      new(_now, 1)
    ];

    Assert.That(_forecast.RunsOutAtUtcFromLastTwoHours(_festival, availableAmount, usage, _now), Is.Null);
  }

  [Test]
  public void RunsOutAtUtcFromLastTwoHours_FestivalStartedFourHoursAgo_CountsOnlyWhatTheLastTwoHoursUsed()
  {
    _festival.StartsAtUtc = _now.AddHours(-4);
    IngredientUsagePoint[] usage =
    [
      new(_now.AddHours(-4), 0),
      new(_now.AddHours(-3), 400),
      new(_now.AddHours(-1), 500),
      new(_now, 600)
    ];

    Assert.That(_forecast.RunsOutAtUtcFromLastTwoHours(_festival, 1000, usage, _now), Is.EqualTo(_now.AddHours(4)));
  }

  [Test]
  public void RunsOutAtUtcFromLastTwoHours_AnOrderExactlyAtTheStartOfTheWindow_CountsItInsideTheWindow()
  {
    _festival.StartsAtUtc = _now.AddHours(-4);
    IngredientUsagePoint[] usage =
    [
      new(_now.AddHours(-4), 0),
      new(_now.AddHours(-2), 400),
      new(_now, 600)
    ];

    Assert.That(_forecast.RunsOutAtUtcFromLastTwoHours(_festival, 1000, usage, _now), Is.EqualTo(_now.AddMinutes(80)));
  }

  [Test]
  public void RunsOutAtUtcFromLastTwoHours_FractionalPortionsThatAddUpToTheAvailableAmount_IsUnknown()
  {
    IngredientUsagePoint[] usage =
    [
      new(_now.AddHours(-2), 0),
      new(_now.AddHours(-1), 0.32999999999999996),
      new(_now, 0.32999999999999996)
    ];

    Assert.That(_forecast.RunsOutAtUtcFromLastTwoHours(_festival, 0.33, usage, _now), Is.Null);
  }

  [Test]
  public void RunsOutAtUtcFromLastTwoHours_FestivalStartedOneHourAgo_CountsFromTheStart()
  {
    _festival.StartsAtUtc = _now.AddHours(-1);
    IngredientUsagePoint[] usage =
    [
      new(_now.AddHours(-1), 0),
      new(_now.AddMinutes(-30), 100),
      new(_now, 100)
    ];

    Assert.That(_forecast.RunsOutAtUtcFromLastTwoHours(_festival, 300, usage, _now), Is.EqualTo(_now.AddHours(2)));
  }

  [Test]
  public void RunsOutAtUtcFromLastTwoHours_NothingUsedInTheLastTwoHours_IsUnknown()
  {
    _festival.StartsAtUtc = _now.AddHours(-4);
    IngredientUsagePoint[] usage =
    [
      new(_now.AddHours(-4), 0),
      new(_now.AddHours(-3), 400),
      new(_now, 400)
    ];

    Assert.That(_forecast.RunsOutAtUtcFromLastTwoHours(_festival, 1000, usage, _now), Is.Null);
  }

  [Test]
  public void RunsOutAtUtcFromLastTwoHours_NoLimit_IsUnknown()
  {
    IngredientUsagePoint[] usage =
    [
      new(_now.AddHours(-2), 0),
      new(_now.AddHours(-1), 300),
      new(_now, 300)
    ];

    Assert.That(_forecast.RunsOutAtUtcFromLastTwoHours(_festival, null, usage, _now), Is.Null);
  }

  [TestCase(300d)]
  [TestCase(200d)]
  public void RunsOutAtUtcFromLastTwoHours_NothingLeft_IsUnknown(double availableAmount)
  {
    IngredientUsagePoint[] usage =
    [
      new(_now.AddHours(-2), 0),
      new(_now.AddHours(-1), 300),
      new(_now, 300)
    ];

    Assert.That(_forecast.RunsOutAtUtcFromLastTwoHours(_festival, availableAmount, usage, _now), Is.Null);
  }

  [Test]
  public void RunsOutAtUtcFromLastTwoHours_HiddenFestival_IsUnknown()
  {
    _festival.IsHidden = true;
    IngredientUsagePoint[] usage =
    [
      new(_now.AddHours(-2), 0),
      new(_now.AddHours(-1), 300),
      new(_now, 300)
    ];

    Assert.That(_forecast.RunsOutAtUtcFromLastTwoHours(_festival, 600, usage, _now), Is.Null);
  }

  [Test]
  public void RunsOutSoonAtUtc_ExactlyTwoHoursFromNow_IsThatMoment()
  {
    Assert.That(_forecast.RunsOutSoonAtUtc(_festival, _now.AddHours(3), _now.AddHours(2), _now), Is.EqualTo(_now.AddHours(2)));
  }

  [Test]
  public void RunsOutSoonAtUtc_JustOverTwoHoursFromNow_IsNone()
  {
    Assert.That(_forecast.RunsOutSoonAtUtc(_festival, _now.AddHours(2).AddSeconds(1), null, _now), Is.Null);
  }

  [Test]
  public void RunsOutSoonAtUtc_TwoForecasts_IsTheEarlierOne()
  {
    Assert.That(_forecast.RunsOutSoonAtUtc(_festival, _now.AddMinutes(90), _now.AddMinutes(60), _now), Is.EqualTo(_now.AddMinutes(60)));
  }

  [Test]
  public void RunsOutSoonAtUtc_OnlyTheTwoHourForecast_IsThatForecast()
  {
    Assert.That(_forecast.RunsOutSoonAtUtc(_festival, null, _now.AddMinutes(60), _now), Is.EqualTo(_now.AddMinutes(60)));
  }

  [TestCase(90)]
  [TestCase(60)]
  public void RunsOutSoonAtUtc_ForecastAtOrAfterTheFestivalsEnd_IsNone(int forecastInMinutes)
  {
    _festival.EndsAtUtc = _now.AddHours(1);

    Assert.That(_forecast.RunsOutSoonAtUtc(_festival, _now.AddMinutes(forecastInMinutes), null, _now), Is.Null);
  }

  [Test]
  public void RunsOutSoonAtUtc_HiddenFestival_IsNone()
  {
    _festival.IsHidden = true;

    Assert.That(_forecast.RunsOutSoonAtUtc(_festival, _now.AddHours(1), _now.AddHours(1), _now), Is.Null);
  }

  [Test]
  public void RunsOutSoonAtUtc_NoForecast_IsNone()
  {
    Assert.That(_forecast.RunsOutSoonAtUtc(_festival, null, null, _now), Is.Null);
  }

  [TestCase(300d)]
  [TestCase(350d)]
  public void StockWarningOf_UsedAsMuchAsIsAvailableOrMore_IsUsedUp(double usedAmount)
  {
    Assert.That(_forecast.StockWarningOf(300, usedAmount, null), Is.EqualTo(StockWarning.UsedUp));
  }

  [Test]
  public void StockWarningOf_FractionalPortionsThatAddUpToTheAvailableAmount_IsUsedUp()
  {
    Assert.That(_forecast.StockWarningOf(0.33, 0.32999999999999996, _now), Is.EqualTo(StockWarning.UsedUp));
  }

  [Test]
  public void RunsOutAtUtcSinceStart_FractionalPortionsThatAddUpToTheAvailableAmount_IsUnknown()
  {
    Assert.That(_forecast.RunsOutAtUtcSinceStart(_festival, 0.33, 0.32999999999999996, _now), Is.Null);
  }

  [Test]
  public void StockWarningOf_AMomentSoon_IsRunsOutSoon()
  {
    Assert.That(_forecast.StockWarningOf(400, 300, _now.AddHours(1)), Is.EqualTo(StockWarning.RunsOutSoon));
  }

  [Test]
  public void StockWarningOf_NoMomentSoon_IsNone()
  {
    Assert.That(_forecast.StockWarningOf(400, 300, null), Is.EqualTo(StockWarning.None));
  }

  [Test]
  public void StockWarningOf_NoLimit_IsNone()
  {
    Assert.That(_forecast.StockWarningOf(null, 300, null), Is.EqualTo(StockWarning.None));
  }
}
