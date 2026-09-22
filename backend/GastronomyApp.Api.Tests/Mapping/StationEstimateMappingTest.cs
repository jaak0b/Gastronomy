using GastronomyApp.Api.Mapping;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using MapsterMapper;

namespace GastronomyApp.Api.Tests.Mapping;

[TestFixture]
public sealed class StationEstimateMappingTest
{
  [SetUp]
  public void SetUp()
  {
    _mapper = new Mapper(new MappingConfiguration(new()).Build());
  }

  private readonly Guid _kitchenId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

  private IMapper _mapper = null!;

  [Test]
  public void Map_AStationWithOpenWork_ReportsTheMinutesStillQueuedThere()
  {
    var station = StationWith(OpenItem(4, false), OpenItem(3.5, false));

    var view = _mapper.Map<StationEstimateView>(station);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.StationId, Is.EqualTo(_kitchenId));
                      Assert.That(view.QueuedMinutes, Is.EqualTo(7.5));
                    });
  }

  [Test]
  public void Map_AnArticlePreparedBesideTheQueue_StaysOutOfTheReportedMinutes()
  {
    var station = StationWith(OpenItem(4, false), OpenItem(30, true));

    Assert.That(_mapper.Map<StationEstimateView>(station).QueuedMinutes, Is.EqualTo(4));
  }

  [Test]
  public void Map_AStationWithoutOpenWork_ReportsNoMinutes()
  {
    Assert.That(_mapper.Map<StationEstimateView>(StationWith()).QueuedMinutes, Is.Zero);
  }

  private Station StationWith(params OrderItem[] items)
  {
    Station station = new()
                      {
                        Id = _kitchenId,
                        Name = "Kueche",
                        SortOrder = 1,
                        IsActive = true
                      };

    var stationOrderId = Guid.NewGuid();

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = Guid.NewGuid(),
                                  FestivalId = Guid.NewGuid(),
                                  StationId = station.Id,
                                  StationOrderNumber = 1,
                                  DeliveryMode = DeliveryMode.Together
                                };

    foreach (var item in items)
    {
      item.StationOrderId = stationOrderId;
      stationOrder.Items.Add(item);
    }

    station.StationOrders.Add(stationOrder);

    return station;
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
}
