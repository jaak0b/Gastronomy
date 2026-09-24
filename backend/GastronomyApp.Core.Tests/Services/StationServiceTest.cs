using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class StationServiceTest
{
  private readonly StationService _stationService = new();
  private readonly DateTime _handedOutAtUtc = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);

  [Test]
  public void QueuedMinutesOf_TwoOpenArticles_SumsTheirStatedMinutes()
  {
    var station = StationWith(OpenItem(5, false), OpenItem(7, false));

    Assert.That(_stationService.QueuedMinutesOf(station), Is.EqualTo(12));
  }

  [Test]
  public void QueuedMinutesOf_HalfMinutes_SumsTheHalves()
  {
    var station = StationWith(OpenItem(1.5, false), OpenItem(2.5, false));

    Assert.That(_stationService.QueuedMinutesOf(station), Is.EqualTo(4));
  }

  [Test]
  public void QueuedMinutesOf_FractionsThatCarryBinaryDust_ReachTheWireWithOneDecimalPlace()
  {
    var station = StationWith(OpenItem(0.1, false), OpenItem(0.2, false));

    Assert.That(_stationService.QueuedMinutesOf(station), Is.EqualTo(0.3));
  }

  [Test]
  public void QueuedMinutesOf_AnArticleWithoutAStatedMinuteCount_CountsAsZero()
  {
    var station = StationWith(OpenItem(null, false), OpenItem(3, false));

    Assert.That(_stationService.QueuedMinutesOf(station), Is.EqualTo(3));
  }

  [Test]
  public void QueuedMinutesOf_AnArticlePreparedBesideTheQueue_StaysOutOfTheSum()
  {
    var station = StationWith(OpenItem(5, false), OpenItem(7, true));

    Assert.That(_stationService.QueuedMinutesOf(station), Is.EqualTo(5));
  }

  [Test]
  public void QueuedMinutesOf_HalfMinutesThatArePreparedBesideTheQueue_StayOutOfTheSum()
  {
    var station = StationWith(OpenItem(1.5, false), OpenItem(2.5, true));

    Assert.That(_stationService.QueuedMinutesOf(station), Is.EqualTo(1.5));
  }

  [Test]
  public void QueuedMinutesOf_OnlyArticlesPreparedBesideTheQueue_LeavesTheQueueEmpty()
  {
    var station = StationWith(OpenItem(4, true), OpenItem(null, true));

    Assert.That(_stationService.QueuedMinutesOf(station), Is.Zero);
  }

  [Test]
  public void QueuedMinutesOf_AnItemTheStationHandedOut_StaysOutOfTheSum()
  {
    var station = StationWith(OpenItem(5, false), HandedOutItem(7));

    Assert.That(_stationService.QueuedMinutesOf(station), Is.EqualTo(5));
  }

  [Test]
  public void QueuedMinutesOf_ItemsOfTwoStationOrders_SumsAcrossBothOfThem()
  {
    var station = StationWith(OpenItem(5, false));
    AddStationOrder(station, OpenItem(3, false));

    Assert.That(_stationService.QueuedMinutesOf(station), Is.EqualTo(8));
  }

  [Test]
  public void QueuedMinutesOf_AStationWithoutOpenWork_IsZero()
  {
    Assert.That(_stationService.QueuedMinutesOf(StationWith()), Is.Zero);
  }

  [Test]
  public void ReadyInMinutesOf_AnArticleThatWaitsInTheQueue_AddsItsOwnMinutesToTheOpenSharedQueue()
  {
    var station = StationWith(OpenItem(5, false), OpenItem(30, true));
    var bratwurst = Article(4, false);

    Assert.That(_stationService.ReadyInMinutesOf(AssignmentOf(bratwurst, station)), Is.EqualTo(9));
  }

  [Test]
  public void ReadyInMinutesOf_AnIndependentArticle_AddsItsOwnMinutesToWhatIsStillOpenOfThatArticleAlone()
  {
    var fries = Article(3, true);
    var station = StationWith(OpenItem(10, false), OpenItemOf(fries), OpenItemOf(fries), OpenItem(20, true));

    Assert.That(_stationService.ReadyInMinutesOf(AssignmentOf(fries, station)), Is.EqualTo(9));
  }

  [Test]
  public void QuotedReadyInMinutesOf_MixedLinesWhereTheIndependentArticleTakesLonger_AnswersWithTheIndependentArticle()
  {
    var bratwurst = Article(4, false);
    var pizza = Article(15, true);
    var station = StationWith(OpenItem(2, false), OpenItemOf(pizza));
    EstimateQuoteLine[] lines =
    [
      new() { CatalogItemId = bratwurst.Id, StationId = station.Id, Units = 1 },
      new() { CatalogItemId = pizza.Id, StationId = station.Id, Units = 1 },
      new() { CatalogItemId = bratwurst.Id, StationId = Guid.NewGuid(), Units = 5 }
    ];

    Assert.That(_stationService.QuotedReadyInMinutesOf(station, lines, [bratwurst, pizza]), Is.EqualTo(30));
  }

  [Test]
  public void QuotedReadyInMinutesOf_MixedLinesWhereTheSharedQueueTakesLonger_AnswersWithTheSharedQueue()
  {
    var bratwurst = Article(4, false);
    var pizza = Article(3, true);
    var station = StationWith(OpenItem(10, false));
    EstimateQuoteLine[] lines =
    [
      new() { CatalogItemId = bratwurst.Id, StationId = station.Id, Units = 2 },
      new() { CatalogItemId = pizza.Id, StationId = station.Id, Units = 1 }
    ];

    Assert.That(_stationService.QuotedReadyInMinutesOf(station, lines, [bratwurst, pizza]), Is.EqualTo(18));
  }

  [Test]
  public void QuotedReadyInMinutesOf_SeveralUnitsOfAnIndependentArticle_MultipliesItsMinutes()
  {
    var pizza = Article(2.5, true);
    var station = StationWith(OpenItem(1, false));
    EstimateQuoteLine[] lines = [new() { CatalogItemId = pizza.Id, StationId = station.Id, Units = 3 }];

    Assert.That(_stationService.QuotedReadyInMinutesOf(station, lines, [pizza]), Is.EqualTo(7.5));
  }

  [Test]
  public void QuotedReadyInMinutesOf_NoLineAtTheStationHasMinutes_AnswersWithoutANumber()
  {
    var beer = Article(null, false);
    var station = StationWith(OpenItem(5, false));
    EstimateQuoteLine[] lines = [new() { CatalogItemId = beer.Id, StationId = station.Id, Units = 2 }];

    Assert.That(_stationService.QuotedReadyInMinutesOf(station, lines, [beer]), Is.Null);
  }

  private Station StationWith(params OrderItem[] items)
  {
    Station station = new()
                      {
                        Id = Guid.NewGuid(),
                        Name = "Kueche",
                        SortOrder = 1,
                        IsActive = true
                      };

    AddStationOrder(station, items);

    return station;
  }

  private void AddStationOrder(Station station, params OrderItem[] items)
  {
    StationOrder stationOrder = new()
                                {
                                  Id = Guid.NewGuid(),
                                  OrderId = Guid.NewGuid(),
                                  FestivalId = Guid.NewGuid(),
                                  StationId = station.Id,
                                  StationOrderNumber = station.StationOrders.Count + 1,
                                  DeliveryMode = DeliveryMode.Together
                                };

    foreach (var item in items)
    {
      item.StationOrderId = stationOrder.Id;
      stationOrder.Items.Add(item);
    }

    station.StationOrders.Add(stationOrder);
  }

  private OrderItem OpenItem(double? productionMinutes, bool isQueueIndependent)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = Guid.NewGuid(),
             CatalogItemId = Guid.NewGuid(),
             ItemName = "Artikel",
             UnitPriceCents = 350,
             CatalogItem = new()
                           {
                             Id = Guid.NewGuid(),
                             Name = "Artikel",
                             CategoryId = Guid.NewGuid(),
                             SortOrder = 1,
                             IsActive = true,
                             ProductionMinutes = productionMinutes,
                             IsQueueIndependent = isQueueIndependent
                           }
           };
  }

  private CatalogItem Article(double? productionMinutes, bool isQueueIndependent)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = "Artikel",
             CategoryId = Guid.NewGuid(),
             SortOrder = 1,
             IsActive = true,
             ProductionMinutes = productionMinutes,
             IsQueueIndependent = isQueueIndependent
           };
  }

  private OrderItem OpenItemOf(CatalogItem article)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = Guid.NewGuid(),
             CatalogItemId = article.Id,
             ItemName = article.Name,
             UnitPriceCents = 350,
             CatalogItem = article
           };
  }

  private ItemStationAssignment AssignmentOf(CatalogItem article, Station station)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             FestivalId = Guid.NewGuid(),
             CatalogItemId = article.Id,
             StationId = station.Id,
             CatalogItem = article,
             Station = station
           };
  }

  private OrderItem HandedOutItem(double? productionMinutes)
  {
    var item = OpenItem(productionMinutes, false);
    item.FulfilledAtUtc = _handedOutAtUtc;

    return item;
  }
}
