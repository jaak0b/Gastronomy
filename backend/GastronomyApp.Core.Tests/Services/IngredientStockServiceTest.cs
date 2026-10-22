using ErrorOr;
using FakeItEasy;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Core.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class IngredientStockServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IIngredientStockRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _clock = new(new(_now));
    _festival = new()
                {
                  Id = Guid.NewGuid(),
                  Name = "Sommerfest",
                  StartsAtUtc = _now.AddHours(-2),
                  EndsAtUtc = _now.AddHours(6),
                  NextOrderNumber = 1,
                  IsHidden = false
                };
    _mustardStock = new()
                    {
                      Id = Guid.NewGuid(),
                      FestivalId = _festival.Id,
                      IngredientId = Guid.NewGuid(),
                      AvailableAmount = null,
                      Ingredient = new()
                                   {
                                     Id = Guid.NewGuid(),
                                     Name = "Senf",
                                     Unit = IngredientUnit.Gram,
                                     IsActive = true
                                   }
                    };

    A.CallTo(() => _festivalRepository.FindByIdAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));
    A.CallTo(() => _festivalRepository.FindByIdAsync(_festival.Id, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(_festival));
    A.CallTo(() => _repository.FindActiveUsedOnTheMenuAsync(_festival.Id, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<FestivalIngredient>>([_mustardStock]));
    A.CallTo(() => _repository.SumConsumedAmountsByOrderTimeAsync(_festival.Id, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyDictionary<DateTime, double>>>(new Dictionary<Guid, IReadOnlyDictionary<DateTime, double>>
                                                                                                {
                                                                                                  [_mustardStock.IngredientId] = new Dictionary<DateTime, double>
                                                                                                                                 {
                                                                                                                                   [_now.AddMinutes(-90)] = 100,
                                                                                                                                   [_now.AddMinutes(-30)] = 200
                                                                                                                                 }
                                                                                                }));
    A.CallTo(() => _repository.FindAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<FestivalIngredient?>(null));
    A.CallTo(() => _repository.FindAsync(_festival.Id, _mustardStock.IngredientId, A<CancellationToken>._)).Returns(Task.FromResult<FestivalIngredient?>(_mustardStock));
    A.CallTo(() => _repository.FindActiveWithAvailableAmountAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<FestivalIngredient>>([]));

    _service = new(_repository, _festivalRepository, new(_repository, new(), NullLogger<StockSoldOutMarker>.Instance), new(), new(), _clock);
  }

  private readonly DateTime _now = new(2026, 9, 26, 20, 0, 0, DateTimeKind.Utc);

  private IIngredientStockRepository _repository = null!;
  private IFestivalRepository _festivalRepository = null!;
  private FakeTimeProvider _clock = null!;
  private Festival _festival = null!;
  private FestivalIngredient _mustardStock = null!;
  private IngredientStockService _service = null!;

  [Test]
  public async Task ListAtFestivalAsync_AnIngredientOnTheMenu_CarriesTheConsumedAmountAndItsUsageFromTheFestivalsOrders()
  {
    _mustardStock.AvailableAmount = 900;

    ErrorOr<IReadOnlyList<IngredientStockLevel>> levels = await _service.ListAtFestivalAsync(_festival.Id, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(levels.Value, Has.Count.EqualTo(1));
                      Assert.That(levels.Value[0].Stock, Is.SameAs(_mustardStock));
                      Assert.That(levels.Value[0].UsedAmount, Is.EqualTo(300));
                      Assert.That(levels.Value[0].Usage,
                                  Is.EqualTo(new IngredientUsagePointResult[]
                                             {
                                               new(_now.AddHours(-2), 0),
                                               new(_now.AddMinutes(-90), 100),
                                               new(_now.AddMinutes(-30), 300),
                                               new(_now, 300)
                                             }));
                      Assert.That(levels.Value[0].RunsOutAtUtcSinceStart, Is.EqualTo(_now.AddHours(4)));
                      Assert.That(levels.Value[0].RunsOutAtUtcFromLastTwoHours, Is.EqualTo(_now.AddHours(4)));
                      Assert.That(levels.Value[0].StockWarning, Is.EqualTo(StockWarning.None));
                      Assert.That(levels.Value[0].RunsOutSoonAtUtc, Is.Null);
                    });
  }

  [Test]
  public async Task ListAtFestivalAsync_StockThatRunsOutWithinTwoHours_WarnsRunsOutSoonAtTheEarlierForecast()
  {
    _festival.StartsAtUtc = _now.AddHours(-4);
    _mustardStock.AvailableAmount = 400;

    ErrorOr<IReadOnlyList<IngredientStockLevel>> levels = await _service.ListAtFestivalAsync(_festival.Id, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(levels.Value[0].RunsOutAtUtcSinceStart, Is.EqualTo(_now.AddMinutes(80)));
                      Assert.That(levels.Value[0].RunsOutAtUtcFromLastTwoHours, Is.EqualTo(_now.AddMinutes(40)));
                      Assert.That(levels.Value[0].StockWarning, Is.EqualTo(StockWarning.RunsOutSoon));
                      Assert.That(levels.Value[0].RunsOutSoonAtUtc, Is.EqualTo(_now.AddMinutes(40)));
                    });
  }

  [Test]
  public async Task ListAtFestivalAsync_UsedUpAtAFestivalThatHasEnded_WarnsUsedUpWithoutAMoment()
  {
    _festival.StartsAtUtc = _now.AddHours(-10);
    _festival.EndsAtUtc = _now.AddHours(-1);
    _mustardStock.AvailableAmount = 300;

    ErrorOr<IReadOnlyList<IngredientStockLevel>> levels = await _service.ListAtFestivalAsync(_festival.Id, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(levels.Value[0].StockWarning, Is.EqualTo(StockWarning.UsedUp));
                      Assert.That(levels.Value[0].RunsOutSoonAtUtc, Is.Null);
                      Assert.That(levels.Value[0].Usage[^1], Is.EqualTo(new IngredientUsagePointResult(_now.AddHours(-1), 300)));
                    });
  }

  [Test]
  public async Task ListAtFestivalAsync_AnUnknownFestival_IsRefusedAsNotFound()
  {
    ErrorOr<IReadOnlyList<IngredientStockLevel>> levels = await _service.ListAtFestivalAsync(Guid.NewGuid(), CancellationToken.None);

    Assert.That(levels.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public void UsageOf_NoOrderYet_RunsFromZeroAtTheStartToZeroNow()
  {
    IReadOnlyList<IngredientUsagePointResult> usage = _service.UsageOf(_festival, new Dictionary<DateTime, double>(), _now);

    Assert.That(usage,
                Is.EqualTo(new IngredientUsagePointResult[]
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

    IReadOnlyList<IngredientUsagePointResult> usage = _service.UsageOf(_festival, consumedAtOrderTimes, _now);

    Assert.That(usage,
                Is.EqualTo(new IngredientUsagePointResult[]
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

    IReadOnlyList<IngredientUsagePointResult> usage = _service.UsageOf(_festival, new Dictionary<DateTime, double>(), _now);

    Assert.That(usage, Is.EqualTo(new IngredientUsagePointResult[] { new(_now.AddHours(1), 0) }));
  }

  [Test]
  public void UsageOf_AfterTheFestivalEnded_EndsAtTheFestivalsEndWithTheUsedAmount()
  {
    _festival.StartsAtUtc = _now.AddHours(-10);
    _festival.EndsAtUtc = _now.AddHours(-1);

    IReadOnlyList<IngredientUsagePointResult> usage = _service.UsageOf(_festival, new Dictionary<DateTime, double> { [_now.AddHours(-5)] = 100 }, _now);

    Assert.That(usage,
                Is.EqualTo(new IngredientUsagePointResult[]
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

    IReadOnlyList<IngredientUsagePointResult> usage = _service.UsageOf(_festival, consumedAtOrderTimes, _now);

    Assert.That(usage,
                Is.EqualTo(new IngredientUsagePointResult[]
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

    IReadOnlyList<IngredientUsagePointResult> usage = _service.UsageOf(_festival, consumedAtOrderTimes, _now);

    Assert.That(usage,
                Is.EqualTo(new IngredientUsagePointResult[]
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

    IReadOnlyList<IngredientUsagePointResult> usage = _service.UsageOf(_festival, consumedAtOrderTimes, _now);

    Assert.That(usage, Is.EqualTo(new IngredientUsagePointResult[] { new(_now.AddHours(1), 100) }));
  }

  [Test]
  public void UsageOf_AnOrderAtThisVeryMoment_EndsWithOnePointForNow()
  {
    IReadOnlyList<IngredientUsagePointResult> usage = _service.UsageOf(_festival, new Dictionary<DateTime, double> { [_now] = 20 }, _now);

    Assert.That(usage,
                Is.EqualTo(new IngredientUsagePointResult[]
                           {
                             new(_now.AddHours(-2), 0),
                             new(_now, 20)
                           }));
  }

  [Test]
  public async Task ListAtFestivalAsync_AnIngredientOnTheMenu_TakesTheUsedAmountFromTheLastPointOfItsUsage()
  {
    ErrorOr<IReadOnlyList<IngredientStockLevel>> levels = await _service.ListAtFestivalAsync(_festival.Id, CancellationToken.None);

    Assert.That(levels.Value[0].UsedAmount, Is.EqualTo(levels.Value[0].Usage[^1].UsedAmount));
  }

  [Test]
  public void RunsOutAtUtcSinceStart_HalfUsedAfterTwoHours_RunsOutTwoHoursFromNow()
  {
    Assert.That(_service.RunsOutAtUtcSinceStart(_festival, 600, 300, _now), Is.EqualTo(_now.AddHours(2)));
  }

  [Test]
  public void RunsOutAtUtcSinceStart_NoLimit_IsUnknown()
  {
    Assert.That(_service.RunsOutAtUtcSinceStart(_festival, null, 300, _now), Is.Null);
  }

  [Test]
  public void RunsOutAtUtcSinceStart_NothingUsedYet_IsUnknown()
  {
    Assert.That(_service.RunsOutAtUtcSinceStart(_festival, 600, 0, _now), Is.Null);
  }

  [TestCase(300d)]
  [TestCase(200d)]
  public void RunsOutAtUtcSinceStart_NothingLeft_IsUnknown(double availableAmount)
  {
    Assert.That(_service.RunsOutAtUtcSinceStart(_festival, availableAmount, 300, _now), Is.Null);
  }

  [TestCase(1d, 6d)]
  [TestCase(-8d, -1d)]
  public void RunsOutAtUtcSinceStart_FestivalNotRunningAtThisTime_IsUnknown(double startsInHours, double endsInHours)
  {
    _festival.StartsAtUtc = _now.AddHours(startsInHours);
    _festival.EndsAtUtc = _now.AddHours(endsInHours);

    Assert.That(_service.RunsOutAtUtcSinceStart(_festival, 600, 300, _now), Is.Null);
  }

  [Test]
  public void RunsOutAtUtcSinceStart_HiddenFestival_IsUnknown()
  {
    _festival.IsHidden = true;

    Assert.That(_service.RunsOutAtUtcSinceStart(_festival, 600, 300, _now), Is.Null);
  }

  [Test]
  public void RunsOutAtUtcSinceStart_AnAmountThatLastsFarIntoTheFuture_IsThatMoment()
  {
    Assert.That(_service.RunsOutAtUtcSinceStart(_festival, 30_000_001, 1, _now), Is.EqualTo(_now + TimeSpan.FromHours(60_000_000)));
  }

  [TestCase(40_000_001d)]
  [TestCase(1e15)]
  public void RunsOutAtUtcSinceStart_AMomentBeyondTheLastRepresentableDate_IsUnknown(double availableAmount)
  {
    Assert.That(_service.RunsOutAtUtcSinceStart(_festival, availableAmount, 1, _now), Is.Null);
  }

  [Test]
  public void RunsOutAtUtcFromLastTwoHours_AnAmountThatLastsFarIntoTheFuture_IsThatMoment()
  {
    IngredientUsagePointResult[] usage =
    [
      new(_now.AddHours(-2), 0),
      new(_now.AddHours(-1), 1),
      new(_now, 1)
    ];

    Assert.That(_service.RunsOutAtUtcFromLastTwoHours(_festival, 30_000_001, usage, _now), Is.EqualTo(_now + TimeSpan.FromHours(60_000_000)));
  }

  [TestCase(40_000_001d)]
  [TestCase(1e15)]
  public void RunsOutAtUtcFromLastTwoHours_AMomentBeyondTheLastRepresentableDate_IsUnknown(double availableAmount)
  {
    IngredientUsagePointResult[] usage =
    [
      new(_now.AddHours(-2), 0),
      new(_now.AddHours(-1), 1),
      new(_now, 1)
    ];

    Assert.That(_service.RunsOutAtUtcFromLastTwoHours(_festival, availableAmount, usage, _now), Is.Null);
  }

  [Test]
  public void RunsOutAtUtcFromLastTwoHours_FestivalStartedFourHoursAgo_CountsOnlyWhatTheLastTwoHoursUsed()
  {
    _festival.StartsAtUtc = _now.AddHours(-4);
    IngredientUsagePointResult[] usage =
    [
      new(_now.AddHours(-4), 0),
      new(_now.AddHours(-3), 400),
      new(_now.AddHours(-1), 500),
      new(_now, 600)
    ];

    Assert.That(_service.RunsOutAtUtcFromLastTwoHours(_festival, 1000, usage, _now), Is.EqualTo(_now.AddHours(4)));
  }

  [Test]
  public void RunsOutAtUtcFromLastTwoHours_AnOrderExactlyAtTheStartOfTheWindow_CountsItInsideTheWindow()
  {
    _festival.StartsAtUtc = _now.AddHours(-4);
    IngredientUsagePointResult[] usage =
    [
      new(_now.AddHours(-4), 0),
      new(_now.AddHours(-2), 400),
      new(_now, 600)
    ];

    Assert.That(_service.RunsOutAtUtcFromLastTwoHours(_festival, 1000, usage, _now), Is.EqualTo(_now.AddMinutes(80)));
  }

  [Test]
  public void RunsOutAtUtcFromLastTwoHours_FractionalPortionsThatAddUpToTheAvailableAmount_IsUnknown()
  {
    IngredientUsagePointResult[] usage =
    [
      new(_now.AddHours(-2), 0),
      new(_now.AddHours(-1), 0.32999999999999996),
      new(_now, 0.32999999999999996)
    ];

    Assert.That(_service.RunsOutAtUtcFromLastTwoHours(_festival, 0.33, usage, _now), Is.Null);
  }

  [Test]
  public void RunsOutAtUtcFromLastTwoHours_FestivalStartedOneHourAgo_CountsFromTheStart()
  {
    _festival.StartsAtUtc = _now.AddHours(-1);
    IngredientUsagePointResult[] usage =
    [
      new(_now.AddHours(-1), 0),
      new(_now.AddMinutes(-30), 100),
      new(_now, 100)
    ];

    Assert.That(_service.RunsOutAtUtcFromLastTwoHours(_festival, 300, usage, _now), Is.EqualTo(_now.AddHours(2)));
  }

  [Test]
  public void RunsOutAtUtcFromLastTwoHours_NothingUsedInTheLastTwoHours_IsUnknown()
  {
    _festival.StartsAtUtc = _now.AddHours(-4);
    IngredientUsagePointResult[] usage =
    [
      new(_now.AddHours(-4), 0),
      new(_now.AddHours(-3), 400),
      new(_now, 400)
    ];

    Assert.That(_service.RunsOutAtUtcFromLastTwoHours(_festival, 1000, usage, _now), Is.Null);
  }

  [Test]
  public void RunsOutAtUtcFromLastTwoHours_NoLimit_IsUnknown()
  {
    IngredientUsagePointResult[] usage =
    [
      new(_now.AddHours(-2), 0),
      new(_now.AddHours(-1), 300),
      new(_now, 300)
    ];

    Assert.That(_service.RunsOutAtUtcFromLastTwoHours(_festival, null, usage, _now), Is.Null);
  }

  [TestCase(300d)]
  [TestCase(200d)]
  public void RunsOutAtUtcFromLastTwoHours_NothingLeft_IsUnknown(double availableAmount)
  {
    IngredientUsagePointResult[] usage =
    [
      new(_now.AddHours(-2), 0),
      new(_now.AddHours(-1), 300),
      new(_now, 300)
    ];

    Assert.That(_service.RunsOutAtUtcFromLastTwoHours(_festival, availableAmount, usage, _now), Is.Null);
  }

  [Test]
  public void RunsOutAtUtcFromLastTwoHours_HiddenFestival_IsUnknown()
  {
    _festival.IsHidden = true;
    IngredientUsagePointResult[] usage =
    [
      new(_now.AddHours(-2), 0),
      new(_now.AddHours(-1), 300),
      new(_now, 300)
    ];

    Assert.That(_service.RunsOutAtUtcFromLastTwoHours(_festival, 600, usage, _now), Is.Null);
  }

  [Test]
  public void RunsOutSoonAtUtc_ExactlyTwoHoursFromNow_IsThatMoment()
  {
    Assert.That(_service.RunsOutSoonAtUtc(_festival, _now.AddHours(3), _now.AddHours(2), _now), Is.EqualTo(_now.AddHours(2)));
  }

  [Test]
  public void RunsOutSoonAtUtc_JustOverTwoHoursFromNow_IsNone()
  {
    Assert.That(_service.RunsOutSoonAtUtc(_festival, _now.AddHours(2).AddSeconds(1), null, _now), Is.Null);
  }

  [Test]
  public void RunsOutSoonAtUtc_TwoForecasts_IsTheEarlierOne()
  {
    Assert.That(_service.RunsOutSoonAtUtc(_festival, _now.AddMinutes(90), _now.AddMinutes(60), _now), Is.EqualTo(_now.AddMinutes(60)));
  }

  [Test]
  public void RunsOutSoonAtUtc_OnlyTheTwoHourForecast_IsThatForecast()
  {
    Assert.That(_service.RunsOutSoonAtUtc(_festival, null, _now.AddMinutes(60), _now), Is.EqualTo(_now.AddMinutes(60)));
  }

  [TestCase(90)]
  [TestCase(60)]
  public void RunsOutSoonAtUtc_ForecastAtOrAfterTheFestivalsEnd_IsNone(int forecastInMinutes)
  {
    _festival.EndsAtUtc = _now.AddHours(1);

    Assert.That(_service.RunsOutSoonAtUtc(_festival, _now.AddMinutes(forecastInMinutes), null, _now), Is.Null);
  }

  [Test]
  public void RunsOutSoonAtUtc_HiddenFestival_IsNone()
  {
    _festival.IsHidden = true;

    Assert.That(_service.RunsOutSoonAtUtc(_festival, _now.AddHours(1), _now.AddHours(1), _now), Is.Null);
  }

  [Test]
  public void RunsOutSoonAtUtc_NoForecast_IsNone()
  {
    Assert.That(_service.RunsOutSoonAtUtc(_festival, null, null, _now), Is.Null);
  }

  [TestCase(300d)]
  [TestCase(350d)]
  public void StockWarningOf_UsedAsMuchAsIsAvailableOrMore_IsUsedUp(double usedAmount)
  {
    Assert.That(_service.StockWarningOf(300, usedAmount, null), Is.EqualTo(StockWarning.UsedUp));
  }

  [Test]
  public void StockWarningOf_FractionalPortionsThatAddUpToTheAvailableAmount_IsUsedUp()
  {
    Assert.That(_service.StockWarningOf(0.33, 0.32999999999999996, _now), Is.EqualTo(StockWarning.UsedUp));
  }

  [Test]
  public void RunsOutAtUtcSinceStart_FractionalPortionsThatAddUpToTheAvailableAmount_IsUnknown()
  {
    Assert.That(_service.RunsOutAtUtcSinceStart(_festival, 0.33, 0.32999999999999996, _now), Is.Null);
  }

  [Test]
  public void StockWarningOf_AMomentSoon_IsRunsOutSoon()
  {
    Assert.That(_service.StockWarningOf(400, 300, _now.AddHours(1)), Is.EqualTo(StockWarning.RunsOutSoon));
  }

  [Test]
  public void StockWarningOf_NoMomentSoon_IsNone()
  {
    Assert.That(_service.StockWarningOf(400, 300, null), Is.EqualTo(StockWarning.None));
  }

  [Test]
  public void StockWarningOf_NoLimit_IsNone()
  {
    Assert.That(_service.StockWarningOf(null, 300, null), Is.EqualTo(StockWarning.None));
  }

  [Test]
  public async Task SetAvailableAmountAsync_AnAmount_StoresItAndChecksTheMenuForThatFestival()
  {
    ErrorOr<FestivalIngredient> saved = await _service.SetAvailableAmountAsync(_festival.Id, _mustardStock.IngredientId, 500, CancellationToken.None);

    Assert.That(saved.Value.AvailableAmount, Is.EqualTo(500));
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly().Then(A.CallTo(() => _repository.FindActiveWithAvailableAmountAsync(_festival.Id, A<CancellationToken>._)).MustHaveHappenedOnceExactly());
  }

  [Test]
  public async Task SetAvailableAmountAsync_NoAmount_StoresUnlimited()
  {
    _mustardStock.AvailableAmount = 500;

    ErrorOr<FestivalIngredient> saved = await _service.SetAvailableAmountAsync(_festival.Id, _mustardStock.IngredientId, null, CancellationToken.None);

    Assert.That(saved.Value.AvailableAmount, Is.Null);
  }

  [TestCase(-1d)]
  [TestCase(double.NaN)]
  [TestCase(double.PositiveInfinity)]
  public async Task SetAvailableAmountAsync_AnAmountBelowZeroOrNotANumber_IsRefusedAsStockInvalid(double availableAmount)
  {
    ErrorOr<FestivalIngredient> saved = await _service.SetAvailableAmountAsync(_festival.Id, _mustardStock.IngredientId, availableAmount, CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(saved.RefusalMessageKey(), Is.EqualTo("errors.admin.ingredients.stockInvalid"));
                      Assert.That(saved.FirstError.NumericType, Is.EqualTo(RefusalType.BadRequest));
                      Assert.That(saved.RefusalMetadata(Refusal.MetadataKeys.ProblemCode), Is.EqualTo("ValidationFailed"));
                      Assert.That(_mustardStock.AvailableAmount, Is.Null);
                    });
    A.CallTo(() => _repository.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task SetAvailableAmountAsync_AnIngredientTheFestivalHasNoRowFor_IsRefusedAsNotFound()
  {
    ErrorOr<FestivalIngredient> saved = await _service.SetAvailableAmountAsync(_festival.Id, Guid.NewGuid(), 5, CancellationToken.None);

    Assert.That(saved.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }
}
