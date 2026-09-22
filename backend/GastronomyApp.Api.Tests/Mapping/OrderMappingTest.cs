using GastronomyApp.Api.Mapping;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Events;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using MapsterMapper;

namespace GastronomyApp.Api.Tests.Mapping;

[TestFixture]
public sealed class OrderMappingTest
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
      new OpenItemMapping(),
      new OrderMapping(new()),
      new SettlementMapping(),
      new StationEstimateMapping(new()),
      new StationQueueMapping(new())
    ];

    _mapper = new Mapper(new MappingConfiguration(registrations).Build());
  }

  private readonly DateTime _placedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly Guid _kitchenId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _barId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

  private IMapper _mapper = null!;

  [Test]
  public void Map_AnOrderNoStationHasHandedOutYet_IsOpenAndCarriesTheTotalThePhoneDisplayed()
  {
    var order = OrderAtOneStation(350, 400);

    var view = _mapper.Map<PlacedOrderView>(order);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.OrderId, Is.EqualTo(order.Id));
                      Assert.That(view.GlobalOrderNumber, Is.EqualTo(7));
                      Assert.That(view.CreatedAtUtc, Is.EqualTo(_placedAtUtc));
                      Assert.That(view.Status, Is.EqualTo(OrderStatus.Open));
                      Assert.That(view.TotalCents, Is.EqualTo(750));
                    });
  }

  [Test]
  public void Map_AnOrderWhereOneItemWasHandedOut_IsPartiallyFulfilled()
  {
    var order = OrderAtOneStation(350, 400);
    order.StationOrders[0].Items[0].FulfilledAtUtc = _placedAtUtc.AddMinutes(4);

    Assert.That(_mapper.Map<PlacedOrderView>(order).Status, Is.EqualTo(OrderStatus.PartiallyFulfilled));
  }

  [Test]
  public void Map_AnOrderAtTwoStations_ListsTheStationOrdersInTheStationsOwnOrder()
  {
    var order = OrderAtOneStation(350);
    order.StationOrders.Clear();
    order.StationOrders.Add(StationOrderAt(order.Id, _barId, "Theke", 2, 400));
    order.StationOrders.Add(StationOrderAt(order.Id, _kitchenId, "Kueche", 1, 350));

    var view = _mapper.Map<PlacedOrderView>(order);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.StationOrders.Select(stationOrder => stationOrder.StationName),
                                  Is.EqualTo(new[]
                                             {
                                               "Kueche",
                                               "Theke"
                                             }));
                      Assert.That(view.StationOrders[0].StationId, Is.EqualTo(_kitchenId));
                      Assert.That(view.StationOrders[0].DeliveryMode, Is.EqualTo(DeliveryMode.Together));
                    });
  }

  [Test]
  public void Map_ItemsOfOneStationOrder_ListsThemByNameAndNote()
  {
    var order = OrderAtOneStation(350);
    var stationOrder = order.StationOrders[0];
    stationOrder.Items.Clear();
    var schnitzel = ItemOf(stationOrder.Id, "Schnitzel", null, 900);
    var plainKasekrainer = ItemOf(stationOrder.Id, "Käsekrainer", null, 400);
    var kasekrainerWithoutKetchup = ItemOf(stationOrder.Id, "Käsekrainer", "Ohne Ketchup", 400);
    stationOrder.Items.Add(schnitzel);
    stationOrder.Items.Add(kasekrainerWithoutKetchup);
    stationOrder.Items.Add(plainKasekrainer);

    var view = _mapper.Map<PlacedOrderView>(order);

    Assert.That(view.StationOrders[0].ItemIds,
                Is.EqualTo(new[]
                           {
                             plainKasekrainer.Id,
                             kasekrainerWithoutKetchup.Id,
                             schnitzel.Id
                           }));
  }

  [Test]
  public void Map_AnOrderWhoseLastItemWasHandedOut_AnnouncesItAsFulfilled()
  {
    var order = OrderAtOneStation(350);
    order.StationOrders[0].Items[0].FulfilledAtUtc = _placedAtUtc.AddMinutes(6);

    var payload = _mapper.Map<OrderStatusChangedEvent>(order);

    Assert.Multiple(() =>
                    {
                      Assert.That(payload.OrderId, Is.EqualTo(order.Id));
                      Assert.That(payload.Status, Is.EqualTo(OrderStatus.Fulfilled));
                    });
  }

  private Order OrderAtOneStation(params int[] unitPricesCents)
  {
    Order order = new()
                  {
                    Id = Guid.NewGuid(),
                    ClientOrderId = Guid.NewGuid(),
                    FestivalId = Guid.NewGuid(),
                    GlobalOrderNumber = 7,
                    StaffMemberId = Guid.NewGuid(),
                    TableName = "Tisch 12",
                    CreatedAtUtc = _placedAtUtc
                  };

    order.StationOrders.Add(StationOrderAt(order.Id, _kitchenId, "Kueche", 1, unitPricesCents));

    return order;
  }

  private StationOrder StationOrderAt(Guid orderId, Guid stationId, string stationName, int sortOrder, params int[] unitPricesCents)
  {
    StationOrder stationOrder = new()
                                {
                                  Id = Guid.NewGuid(),
                                  OrderId = orderId,
                                  FestivalId = Guid.NewGuid(),
                                  StationId = stationId,
                                  StationOrderNumber = 3,
                                  DeliveryMode = DeliveryMode.Together,
                                  Station = new()
                                            {
                                              Id = stationId,
                                              Name = stationName,
                                              SortOrder = sortOrder,
                                              IsActive = true
                                            }
                                };

    foreach (var unitPriceCents in unitPricesCents)
      stationOrder.Items.Add(ItemOf(stationOrder.Id, "Bratwurst", null, unitPriceCents));

    return stationOrder;
  }

  private OrderItem ItemOf(Guid stationOrderId, string itemName, string? note, int unitPriceCents)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = stationOrderId,
             CatalogItemId = Guid.NewGuid(),
             ItemName = itemName,
             Note = note,
             UnitPriceCents = unitPriceCents
           };
  }
}
