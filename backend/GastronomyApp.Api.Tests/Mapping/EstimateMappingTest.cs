using GastronomyApp.Api.Mapping;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using MapsterMapper;

namespace GastronomyApp.Api.Tests.Mapping;

[TestFixture]
public sealed class EstimateMappingTest
{
  [SetUp]
  public void SetUp()
  {
    IMappingRegistration[] registrations =
    [
      new AdminCategoryMapping(),
      new AdminFestivalMapping(new()),
      new AdminItemMapping(),
      new AdminStaffMembersMapping(new()),
      new AdminStationMapping(new()),
      new CatalogMapping(),
      new EnrolmentMapping(),
      new EstimateMapping(new()),
      new OpenItemMapping(),
      new OrderMapping(new()),
      new SettlementMapping(),
      new StationQueueMapping(new())
    ];

    _mapper = new Mapper(new MappingConfiguration(registrations).Build());
  }

  private IMapper _mapper = null!;

  [Test]
  public void Map_ATimedAssignmentOfAnArticleThatWaitsInTheQueue_ReportsTheOpenSharedQueuePlusItsOwnMinutes()
  {
    var station = StationWith(OpenItem(5, false), OpenItem(30, true));
    var bratwurst = Article(4, false);

    var view = _mapper.Map<ItemEstimateView>(AssignmentOf(bratwurst, station));

    Assert.Multiple(() =>
                    {
                      Assert.That(view.CatalogItemId, Is.EqualTo(bratwurst.Id));
                      Assert.That(view.StationId, Is.EqualTo(station.Id));
                      Assert.That(view.ReadyInMinutes, Is.EqualTo(9));
                    });
  }

  [Test]
  public void Map_ATimedAssignmentOfAnIndependentArticle_ReportsWhatIsStillOpenOfThatArticleAlonePlusItsOwnMinutes()
  {
    var fries = Article(3, true);
    var station = StationWith(OpenItem(10, false), OpenItemOf(fries), OpenItemOf(fries), OpenItem(20, true));

    var view = _mapper.Map<ItemEstimateView>(AssignmentOf(fries, station));

    Assert.That(view.ReadyInMinutes, Is.EqualTo(9));
  }

  [Test]
  public void Map_AStationQuoteWithoutATimedLine_CarriesANullStationTime()
  {
    var stationId = Guid.NewGuid();
    var quote = new StationQuote(stationId, null);

    var view = _mapper.Map<StationQuoteView>(quote);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.StationId, Is.EqualTo(stationId));
                      Assert.That(view.ReadyInMinutes, Is.Null);
                    });
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

    StationOrder stationOrder = new()
                                {
                                  Id = Guid.NewGuid(),
                                  OrderId = Guid.NewGuid(),
                                  FestivalId = Guid.NewGuid(),
                                  StationId = station.Id,
                                  StationOrderNumber = 1,
                                  DeliveryMode = DeliveryMode.Together
                                };

    foreach (var item in items)
    {
      item.StationOrderId = stationOrder.Id;
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
}
