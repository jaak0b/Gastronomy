using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Mapster;

namespace GastronomyApp.Infrastructure.Tests.Mapping;

[TestFixture]
public sealed class OrderMappingTest
{
  [SetUp]
  public void SetUp()
  {
    _mapping = new CoreMappingFactory().Create();
  }

  private readonly DateTime _placedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly Guid _kitchenId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _barId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

  private TypeAdapterConfig _mapping = null!;

  [Test]
  public void Map_AnOrderNoStationHasHandedOutYet_IsOpenAndCarriesTheTotalThePhoneDisplayed()
  {
    var order = OrderAtOneStation(350, 400);

    var view = order.Adapt<PlacedOrderView>(_mapping);

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

    Assert.That(order.Adapt<PlacedOrderView>(_mapping).Status, Is.EqualTo(OrderStatus.PartiallyFulfilled));
  }

  [Test]
  public void Map_AnOrderAtTwoStations_ListsTheStationOrdersInTheStationsOwnOrder()
  {
    var order = OrderAtOneStation(350);
    order.StationOrders.Clear();
    order.StationOrders.Add(StationOrderAt(order.Id, _barId, "Theke", 2, 400));
    order.StationOrders.Add(StationOrderAt(order.Id, _kitchenId, "Kueche", 1, 350));

    var view = order.Adapt<PlacedOrderView>(_mapping);

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

    var view = order.Adapt<PlacedOrderView>(_mapping);

    Assert.That(view.StationOrders[0].ItemIds,
                Is.EqualTo(new[]
                           {
                             plainKasekrainer.Id,
                             kasekrainerWithoutKetchup.Id,
                             schnitzel.Id
                           }));
  }

  [Test]
  public void Map_NamesInMixedCase_AreListedWithCapitalsIgnoredAndUmlautsAfterZ()
  {
    var order = OrderAtOneStation(350);
    var stationOrder = order.StationOrders[0];
    stationOrder.Items.Clear();
    var zwiebel = ItemOf(stationOrder.Id, "Zwiebel", null, 100);
    var aepfel = ItemOf(stationOrder.Id, "Äpfel", null, 100);
    var apfel = ItemOf(stationOrder.Id, "apfel", null, 100);
    var birne = ItemOf(stationOrder.Id, "Birne", null, 100);
    stationOrder.Items.Add(zwiebel);
    stationOrder.Items.Add(aepfel);
    stationOrder.Items.Add(apfel);
    stationOrder.Items.Add(birne);
    stationOrder.Station.SortOrder = 1;
    stationOrder.Station.Name = "Zapfhahn";
    order.StationOrders.Add(StationOrderAt(order.Id, Guid.NewGuid(), "grill", 1, 400));
    order.StationOrders.Add(StationOrderAt(order.Id, Guid.NewGuid(), "Bar", 1, 400));

    var view = order.Adapt<PlacedOrderView>(_mapping);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.StationOrders.Select(placed => placed.StationName),
                                  Is.EqualTo(new[]
                                             {
                                               "Bar",
                                               "grill",
                                               "Zapfhahn"
                                             }));
                      Assert.That(view.StationOrders.Single(placed => placed.StationName == "Zapfhahn").ItemIds,
                                  Is.EqualTo(new[]
                                             {
                                               apfel.Id,
                                               birne.Id,
                                               zwiebel.Id,
                                               aepfel.Id
                                             }));
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
                    TableId = Guid.NewGuid(),
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
