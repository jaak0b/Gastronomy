using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class StationWaitingTimesTest
{
  private readonly StationWaitingTimes _stationWaitingTimes = new();
  private readonly Guid _stationId = Guid.NewGuid();

  [Test]
  public void QueuedMinutesOf_TwoOpenArticles_SumsTheirStatedMinutes()
  {
    var station = StationWith(OpenItem(5, false), OpenItem(7, false));

    Assert.That(_stationWaitingTimes.QueuedMinutesOf(station), Is.EqualTo(12));
  }

  [Test]
  public void QueuedMinutesOf_HalfMinutes_SumsTheHalves()
  {
    var station = StationWith(OpenItem(1.5, false), OpenItem(2.5, false));

    Assert.That(_stationWaitingTimes.QueuedMinutesOf(station), Is.EqualTo(4));
  }

  [Test]
  public void QueuedMinutesOf_FractionsThatCarryBinaryDust_ReachTheWireWithOneDecimalPlace()
  {
    var station = StationWith(OpenItem(0.1, false), OpenItem(0.2, false));

    Assert.That(_stationWaitingTimes.QueuedMinutesOf(station), Is.EqualTo(0.3));
  }

  [Test]
  public void QueuedMinutesOf_AnArticleWithoutAStatedMinuteCount_CountsAsZero()
  {
    var station = StationWith(OpenItem(null, false), OpenItem(3, false));

    Assert.That(_stationWaitingTimes.QueuedMinutesOf(station), Is.EqualTo(3));
  }

  [Test]
  public void QueuedMinutesOf_AnArticlePreparedBesideTheQueue_StaysOutOfTheSum()
  {
    var station = StationWith(OpenItem(5, false), OpenItem(7, true));

    Assert.That(_stationWaitingTimes.QueuedMinutesOf(station), Is.EqualTo(5));
  }

  [Test]
  public void QueuedMinutesOf_HalfMinutesThatArePreparedBesideTheQueue_StayOutOfTheSum()
  {
    var station = StationWith(OpenItem(1.5, false), OpenItem(2.5, true));

    Assert.That(_stationWaitingTimes.QueuedMinutesOf(station), Is.EqualTo(1.5));
  }

  [Test]
  public void QueuedMinutesOf_OnlyArticlesPreparedBesideTheQueue_LeavesTheQueueEmpty()
  {
    var station = StationWith(OpenItem(4, true), OpenItem(null, true));

    Assert.That(_stationWaitingTimes.QueuedMinutesOf(station), Is.Zero);
  }

  [Test]
  public void QueuedMinutesOf_AStationWithoutOpenWork_IsZero()
  {
    Assert.That(_stationWaitingTimes.QueuedMinutesOf(StationWith()), Is.Zero);
  }

  [Test]
  public void ReadyInMinutesOf_AnArticleThatWaitsInTheQueue_AddsItsOwnMinutesToTheOpenSharedQueue()
  {
    var station = StationWith(OpenItem(5, false), OpenItem(30, true));
    var bratwurst = Article(4, false);

    Assert.That(_stationWaitingTimes.ReadyInMinutesOf(bratwurst, station), Is.EqualTo(9));
  }

  [Test]
  public void ReadyInMinutesOf_AnIndependentArticle_AddsItsOwnMinutesToWhatIsStillOpenOfThatArticleAlone()
  {
    var fries = Article(3, true);
    var station = StationWith(OpenItem(10, false), OpenItemOf(fries), OpenItemOf(fries), OpenItem(20, true));

    Assert.That(_stationWaitingTimes.ReadyInMinutesOf(fries, station), Is.EqualTo(9));
  }

  [Test]
  public void QuotedReadyInMinutesOf_MixedLinesWhereTheIndependentArticleTakesLonger_AnswersWithTheIndependentArticle()
  {
    var bratwurst = Article(4, false);
    var pizza = Article(15, true);
    var station = StationWith(OpenItem(2, false), OpenItemOf(pizza));
    EstimateQuoteLine[] lines =
    [
      new()
      {
        CatalogItemId = bratwurst.CatalogItemId,
        StationId = _stationId,
        Units = 1
      },
      new()
      {
        CatalogItemId = pizza.CatalogItemId,
        StationId = _stationId,
        Units = 1
      },
      new()
      {
        CatalogItemId = bratwurst.CatalogItemId,
        StationId = Guid.NewGuid(),
        Units = 5
      }
    ];

    Assert.That(_stationWaitingTimes.QuotedReadyInMinutesOf(station,
                                                            lines,
                                                            [
                                                              bratwurst,
                                                              pizza
                                                            ]),
                Is.EqualTo(30));
  }

  [Test]
  public void QuotedReadyInMinutesOf_MixedLinesWhereTheSharedQueueTakesLonger_AnswersWithTheSharedQueue()
  {
    var bratwurst = Article(4, false);
    var pizza = Article(3, true);
    var station = StationWith(OpenItem(10, false));
    EstimateQuoteLine[] lines =
    [
      new()
      {
        CatalogItemId = bratwurst.CatalogItemId,
        StationId = _stationId,
        Units = 2
      },
      new()
      {
        CatalogItemId = pizza.CatalogItemId,
        StationId = _stationId,
        Units = 1
      }
    ];

    Assert.That(_stationWaitingTimes.QuotedReadyInMinutesOf(station,
                                                            lines,
                                                            [
                                                              bratwurst,
                                                              pizza
                                                            ]),
                Is.EqualTo(18));
  }

  [Test]
  public void QuotedReadyInMinutesOf_SeveralUnitsOfAnIndependentArticle_MultipliesItsMinutes()
  {
    var pizza = Article(2.5, true);
    var station = StationWith(OpenItem(1, false));
    EstimateQuoteLine[] lines =
    [
      new()
      {
        CatalogItemId = pizza.CatalogItemId,
        StationId = _stationId,
        Units = 3
      }
    ];

    Assert.That(_stationWaitingTimes.QuotedReadyInMinutesOf(station, lines, [pizza]), Is.EqualTo(7.5));
  }

  [Test]
  public void QuotedReadyInMinutesOf_NoLineAtTheStationHasMinutes_AnswersWithoutANumber()
  {
    var beer = Article(null, false);
    var station = StationWith(OpenItem(5, false));
    EstimateQuoteLine[] lines =
    [
      new()
      {
        CatalogItemId = beer.CatalogItemId,
        StationId = _stationId,
        Units = 2
      }
    ];

    Assert.That(_stationWaitingTimes.QuotedReadyInMinutesOf(station, lines, [beer]), Is.Null);
  }

  private OpenWork[] StationWith(params OpenWork[] work)
  {
    return work;
  }

  private OpenWork OpenItem(double? productionMinutes, bool isQueueIndependent)
  {
    return new(_stationId, Guid.NewGuid(), productionMinutes ?? 0, isQueueIndependent);
  }

  private OpenWork OpenItemOf(ArticleTiming article)
  {
    return new(_stationId, article.CatalogItemId, article.ProductionMinutes ?? 0, article.IsQueueIndependent);
  }

  private ArticleTiming Article(double? productionMinutes, bool isQueueIndependent)
  {
    return new(Guid.NewGuid(), productionMinutes, isQueueIndependent);
  }
}
