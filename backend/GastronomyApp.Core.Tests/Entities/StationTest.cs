using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Tests.Entities;

[TestFixture]
public sealed class StationTest
{
  private readonly DateTime _handedOutAtUtc = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);

  [Test]
  public void QueuedMinutes_TwoOpenArticles_SumsTheirStatedMinutes()
  {
    var station = StationWith(OpenItem(5, false), OpenItem(7, false));

    Assert.That(station.QueuedMinutes(), Is.EqualTo(12));
  }

  [Test]
  public void QueuedMinutes_HalfMinutes_SumsTheHalves()
  {
    var station = StationWith(OpenItem(1.5, false), OpenItem(2.5, false));

    Assert.That(station.QueuedMinutes(), Is.EqualTo(4));
  }

  [Test]
  public void QueuedMinutes_FractionsThatCarryBinaryDust_ReachTheWireWithOneDecimalPlace()
  {
    var station = StationWith(OpenItem(0.1, false), OpenItem(0.2, false));

    Assert.That(station.QueuedMinutes(), Is.EqualTo(0.3));
  }

  [Test]
  public void QueuedMinutes_AnArticleWithoutAStatedMinuteCount_CountsAsZero()
  {
    var station = StationWith(OpenItem(null, false), OpenItem(3, false));

    Assert.That(station.QueuedMinutes(), Is.EqualTo(3));
  }

  [Test]
  public void QueuedMinutes_AnArticlePreparedBesideTheQueue_StaysOutOfTheSum()
  {
    var station = StationWith(OpenItem(5, false), OpenItem(7, true));

    Assert.That(station.QueuedMinutes(), Is.EqualTo(5));
  }

  [Test]
  public void QueuedMinutes_HalfMinutesThatArePreparedBesideTheQueue_StayOutOfTheSum()
  {
    var station = StationWith(OpenItem(1.5, false), OpenItem(2.5, true));

    Assert.That(station.QueuedMinutes(), Is.EqualTo(1.5));
  }

  [Test]
  public void QueuedMinutes_OnlyArticlesPreparedBesideTheQueue_LeavesTheQueueEmpty()
  {
    var station = StationWith(OpenItem(4, true), OpenItem(null, true));

    Assert.That(station.QueuedMinutes(), Is.Zero);
  }

  [Test]
  public void QueuedMinutes_AnItemTheStationHandedOut_StaysOutOfTheSum()
  {
    var station = StationWith(OpenItem(5, false), HandedOutItem(7));

    Assert.That(station.QueuedMinutes(), Is.EqualTo(5));
  }

  [Test]
  public void QueuedMinutes_ItemsOfTwoStationOrders_SumsAcrossBothOfThem()
  {
    var station = StationWith(OpenItem(5, false));
    AddStationOrder(station, OpenItem(3, false));

    Assert.That(station.QueuedMinutes(), Is.EqualTo(8));
  }

  [Test]
  public void QueuedMinutes_AStationWithoutOpenWork_IsZero()
  {
    Assert.That(StationWith().QueuedMinutes(), Is.Zero);
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

  private OrderItem HandedOutItem(double? productionMinutes)
  {
    var item = OpenItem(productionMinutes, false);
    item.FulfilledAtUtc = _handedOutAtUtc;

    return item;
  }
}
