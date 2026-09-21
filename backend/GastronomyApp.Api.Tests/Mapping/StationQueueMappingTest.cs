using GastronomyApp.Api.Mapping;
using GastronomyApp.Contracts;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using MapsterMapper;

namespace GastronomyApp.Api.Tests.Mapping;

[TestFixture]
public sealed class StationQueueMappingTest
{
  [SetUp]
  public void SetUp()
  {
    _mapper = new Mapper(new MappingConfiguration().Build());
  }

  private readonly DateTime _placedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly Guid _kitchenId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

  private IMapper _mapper = null!;

  [Test]
  public void Map_AStationOrderWithOpenItems_CarriesTheTableTheWaiterAndBothCounts()
  {
    var station = Kitchen();
    var stationOrder = AddStationOrder(station, 1, DeliveryMode.Together, Item("Bratwurst", null), Item("Limonade", null));
    stationOrder.Items[0].FulfilledAtUtc = _placedAtUtc.AddMinutes(3);

    var view = _mapper.Map<StationQueueView>(station);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.Station.Id, Is.EqualTo(_kitchenId));
                      Assert.That(view.Station.Name, Is.EqualTo("Kueche"));
                      Assert.That(view.Orders, Has.Count.EqualTo(1));
                      Assert.That(view.Orders[0].StationOrderId, Is.EqualTo(stationOrder.Id));
                      Assert.That(view.Orders[0].GlobalOrderNumber, Is.EqualTo(7));
                      Assert.That(view.Orders[0].TableName, Is.EqualTo("Tisch 12"));
                      Assert.That(view.Orders[0].StaffMemberName, Is.EqualTo("Anna"));
                      Assert.That(view.Orders[0].CreatedAtUtc, Is.EqualTo(_placedAtUtc));
                      Assert.That(view.Orders[0].ItemCount, Is.EqualTo(2));
                      Assert.That(view.Orders[0].FulfilledItemCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public void Map_SeveralStationOrders_ListsThemInTheOrderTheStationNumbersThem()
  {
    var station = Kitchen();
    AddStationOrder(station, 2, DeliveryMode.Together, Item("Bratwurst", null));
    AddStationOrder(station, 1, DeliveryMode.Together, Item("Limonade", null));

    var view = _mapper.Map<StationQueueView>(station);

    Assert.That(view.Orders.Select(stationOrder => stationOrder.StationOrderNumber),
                Is.EqualTo(new[]
                           {
                             1,
                             2
                           }));
  }

  [Test]
  public void Map_AnAsItComesOrder_AlsoStandsInTheSecondColumn()
  {
    var station = Kitchen();
    AddStationOrder(station, 1, DeliveryMode.Together, Item("Bratwurst", null));
    AddStationOrder(station, 2, DeliveryMode.AsItComes, Item("Limonade", null));

    var view = _mapper.Map<StationQueueView>(station);

    Assert.That(view.AsItComes.Select(stationOrder => stationOrder.StationOrderNumber), Is.EqualTo(new[] { 2 }));
  }

  [Test]
  public void Map_AnAsItComesOrderTheEmployeeHid_LeavesItOutOfTheSecondColumnOnly()
  {
    var station = Kitchen();
    var stationOrder = AddStationOrder(station, 1, DeliveryMode.AsItComes, Item("Bratwurst", null));
    stationOrder.IsHiddenFromAsItComesQueue = true;

    var view = _mapper.Map<StationQueueView>(station);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.Orders, Has.Count.EqualTo(1));
                      Assert.That(view.AsItComes, Is.Empty);
                    });
  }

  [Test]
  public void Map_ATogetherOrderMarkedHidden_StaysOutOfTheSecondColumnAllTheSame()
  {
    var station = Kitchen();
    var stationOrder = AddStationOrder(station, 1, DeliveryMode.Together, Item("Bratwurst", null));
    stationOrder.IsHiddenFromAsItComesQueue = true;

    Assert.That(_mapper.Map<StationQueueView>(station).AsItComes, Is.Empty);
  }

  [Test]
  public void Map_ItemsOfOneStationOrder_ComeBackTogetherByNameAndNote()
  {
    var station = Kitchen();
    AddStationOrder(station, 1, DeliveryMode.Together, ItemWithId("Käsekrainer", null, "00000000-0000-0000-0000-000000000002"), ItemWithId("Schnitzel", null, "00000000-0000-0000-0000-000000000001"), ItemWithId("Käsekrainer", "Ohne Ketchup", "00000000-0000-0000-0000-000000000003"));

    var view = _mapper.Map<StationQueueView>(station);

    Assert.Multiple(() =>
                    {
                      Assert.That(view.Orders[0].Items.Select(item => item.ItemName),
                                  Is.EqualTo(new[]
                                             {
                                               "Käsekrainer",
                                               "Käsekrainer",
                                               "Schnitzel"
                                             }));
                      Assert.That(view.Orders[0].Items.Select(item => item.Note),
                                  Is.EqualTo(new[]
                                             {
                                               null,
                                               "Ohne Ketchup",
                                               null
                                             }));
                    });
  }

  [Test]
  public void Map_AStationOrderTheStationHandedOut_CarriesEveryLineWithItsHandOverTime()
  {
    var station = Kitchen();
    var stationOrder = AddStationOrder(station, 1, DeliveryMode.Together, Item("Bratwurst", "Ohne Ketchup"));
    stationOrder.Items[0].FulfilledAtUtc = _placedAtUtc.AddMinutes(6);

    IReadOnlyList<StationOrderQueueView> views = _mapper.Map<IReadOnlyList<StationOrderQueueView>>(station.StationOrders.ToList());

    Assert.Multiple(() =>
                    {
                      Assert.That(views[0].Items[0].OrderItemId, Is.EqualTo(stationOrder.Items[0].Id));
                      Assert.That(views[0].Items[0].ItemName, Is.EqualTo("Bratwurst"));
                      Assert.That(views[0].Items[0].Note, Is.EqualTo("Ohne Ketchup"));
                      Assert.That(views[0].Items[0].FulfilledAtUtc, Is.EqualTo(_placedAtUtc.AddMinutes(6)));
                    });
  }

  private Station Kitchen()
  {
    return new()
           {
             Id = _kitchenId,
             Name = "Kueche",
             SortOrder = 1,
             IsActive = true
           };
  }

  private StationOrder AddStationOrder(Station station, int stationOrderNumber, DeliveryMode deliveryMode, params OrderItem[] items)
  {
    var stationOrderId = Guid.NewGuid();

    StationOrder stationOrder = new()
                                {
                                  Id = stationOrderId,
                                  OrderId = Guid.NewGuid(),
                                  FestivalId = Guid.NewGuid(),
                                  StationId = station.Id,
                                  StationOrderNumber = stationOrderNumber,
                                  DeliveryMode = deliveryMode,
                                  Order = Order()
                                };

    foreach (var item in items)
    {
      item.StationOrderId = stationOrderId;
      stationOrder.Items.Add(item);
    }

    station.StationOrders.Add(stationOrder);

    return stationOrder;
  }

  private Order Order()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             ClientOrderId = Guid.NewGuid(),
             FestivalId = Guid.NewGuid(),
             GlobalOrderNumber = 7,
             StaffMemberId = Guid.NewGuid(),
             TableName = "Tisch 12",
             CreatedAtUtc = _placedAtUtc,
             StaffMember = new()
                           {
                             Id = Guid.NewGuid(),
                             Name = "Anna",
                             IsActive = true,
                             CreatedAtUtc = _placedAtUtc
                           }
           };
  }

  private OrderItem Item(string itemName, string? note)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = Guid.NewGuid(),
             CatalogItemId = Guid.NewGuid(),
             ItemName = itemName,
             UnitPriceCents = 350,
             Note = note
           };
  }

  private OrderItem ItemWithId(string itemName, string? note, string id)
  {
    var item = Item(itemName, note);
    item.Id = new(id);

    return item;
  }
}
